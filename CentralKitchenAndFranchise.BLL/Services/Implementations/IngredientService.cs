using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.UnitOfWork;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Ingredients;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Ingredients;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class IngredientService : IIngredientService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public IngredientService(IUnitOfWork uow, AppDbContext db, ICurrentUserService current)
    {
        _uow = uow;
        _db = db;
        _current = current;
    }

    public async Task<PagedResult<IngredientResponse>> SearchAsync(IngredientListQuery query, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        if (pageSize > 200) pageSize = 200;

        var status = (query.Status ?? IngredientStatus.Active).Trim().ToUpperInvariant();
        if (status is not (IngredientStatus.Active or IngredientStatus.Inactive or "ALL"))
            throw new ArgumentException("status must be ACTIVE, INACTIVE, or ALL.");

        var sortBy = (query.SortBy ?? "name").Trim();
        var sortDir = (query.SortDir ?? "asc").Trim().ToLowerInvariant();
        if (sortDir is not ("asc" or "desc"))
            throw new ArgumentException("sortDir must be asc or desc.");

        IQueryable<Ingredient> q = _db.Ingredients.AsNoTracking();

        if (status != "ALL")
            q = q.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(query.Unit))
        {
            var unit = query.Unit.Trim();
            q = q.Where(x => x.Unit == unit);
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            q = q.Where(x => EF.Functions.ILike(x.Name, $"%{term}%"));
        }

        var total = await q.CountAsync(ct);

        q = ApplySort(q, sortBy, sortDir);

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<IngredientResponse>.Create(
            items: items.Select(ToDto).ToList(),
            page: page,
            pageSize: pageSize,
            totalItems: total
        );
    }

    public async Task<IngredientResponse> GetByIdAsync(int id, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        var entity = await _uow.Ingredients.GetByIdAsync(id, ct);
        if (entity is null) throw new KeyNotFoundException($"Ingredient {id} not found.");
        return ToDto(entity);
    }

    public async Task<IngredientResponse> CreateAsync(CreateIngredientRequest request, CancellationToken ct = default)
    {
        RequireAdminOrManager();

        if (request.SupplierId.HasValue)
        {
            var supplierExists = await _db.Suppliers.AsNoTracking()
                .AnyAsync(x => x.SupplierId == request.SupplierId.Value);

            if (!supplierExists)
                throw new KeyNotFoundException($"Supplier {request.SupplierId.Value} not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Name is required.");
        if (string.IsNullOrWhiteSpace(request.Unit)) throw new ArgumentException("Unit is required.");
        if (request.Price < 0) throw new ArgumentException("Price must be >= 0.");

        if (request.ShelfLifeDays <= 0)
            throw new ArgumentException("ShelfLifeDays must be greater than or equal to 0.");

        var now = DateTime.UtcNow;

        var entity = new Ingredient
        {
            SupplierId = request.SupplierId,
            Name = request.Name.Trim(),
            Unit = request.Unit.Trim(),
            Status = IngredientStatus.Active,

            Price = request.Price, 

            SafetyStock = request.SafetyStock,
            WasteThreshold = request.WasteThreshold,
            CreatedAt = now,
            UpdatedAt = now,
            ShelfLifeDays = request.ShelfLifeDays,
        };

        await _uow.Ingredients.AddAsync(entity, ct);

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Ingredient name already exists.");
        }

        await AddAuditAsync(
            action: "INGREDIENT_CREATE",
            entityName: "Ingredient",
            entityId: entity.IngredientId,
            oldObj: null,
            newObj: entity,
            reason: null,
            ct: ct);

        return ToDto(entity);
    }

    public async Task<IngredientResponse> UpdateAsync(int id, UpdateIngredientRequest request, CancellationToken ct = default)
    {
        RequireAdminOrManager();



        var entity = await _uow.Ingredients.GetByIdAsync(id, ct);
        if (entity is null) throw new KeyNotFoundException($"Ingredient {id} not found.");
        if (request.ShelfLifeDays <= 0)
            throw new ArgumentException("ShelfLifeDays must be greater than 0.");
        if (request.Price < 0) throw new ArgumentException("Price must be >= 0.");

        var old = new
        {
            entity.Name,
            entity.Unit,
            entity.Status,
            entity.Price,         
            entity.SafetyStock,
            entity.WasteThreshold,
            entity.ShelfLifeDays,
        };

        entity.Name = request.Name.Trim();
        entity.Unit = request.Unit.Trim();
        entity.Price = request.Price; 
        entity.SafetyStock = request.SafetyStock;
        entity.WasteThreshold = request.WasteThreshold;
        entity.ShelfLifeDays = request.ShelfLifeDays;
        entity.UpdatedAt = DateTime.UtcNow;

        _uow.Ingredients.Update(entity);

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Ingredient name already exists.");
        }

        await AddAuditAsync(
            action: "INGREDIENT_UPDATE",
            entityName: "Ingredient",
            entityId: entity.IngredientId,
            oldObj: old,
            newObj: entity,
            reason: null,
            ct: ct);

        return ToDto(entity);
    }

    public async Task<IngredientResponse> ChangeStatusAsync(int id, ChangeIngredientStatusRequest request, CancellationToken ct = default)
    {
        RequireAdminOrManager();

        var entity = await _uow.Ingredients.GetByIdAsync(id, ct);
        if (entity is null) throw new KeyNotFoundException($"Ingredient {id} not found.");

        var newStatus = request.Status.Trim().ToUpperInvariant();
        if (newStatus is not (IngredientStatus.Active or IngredientStatus.Inactive))
            throw new ArgumentException("Status must be ACTIVE or INACTIVE.");

        var old = new { entity.Status };

        entity.Status = newStatus;
        entity.UpdatedAt = DateTime.UtcNow;

        _uow.Ingredients.Update(entity);
        await _uow.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "INGREDIENT_CHANGE_STATUS",
            entityName: "Ingredient",
            entityId: entity.IngredientId,
            oldObj: old,
            newObj: new { entity.Status },
            reason: string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            ct: ct);

        return ToDto(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await ChangeStatusAsync(id, new ChangeIngredientStatusRequest
        {
            Status = IngredientStatus.Inactive,
            Reason = "Deactivated via DELETE endpoint"
        }, ct);
    }

    private static IQueryable<Ingredient> ApplySort(IQueryable<Ingredient> q, string sortBy, string sortDir)
    {
        var desc = sortDir == "desc";

        return sortBy.ToLowerInvariant() switch
        {
            "id" => desc ? q.OrderByDescending(x => x.IngredientId) : q.OrderBy(x => x.IngredientId),

            "name" => desc ? q.OrderByDescending(x => x.Name).ThenByDescending(x => x.IngredientId)
                           : q.OrderBy(x => x.Name).ThenBy(x => x.IngredientId),

            "unit" => desc ? q.OrderByDescending(x => x.Unit).ThenByDescending(x => x.IngredientId)
                           : q.OrderBy(x => x.Unit).ThenBy(x => x.IngredientId),

            "price" => desc ? q.OrderByDescending(x => x.Price).ThenByDescending(x => x.IngredientId)
                            : q.OrderBy(x => x.Price).ThenBy(x => x.IngredientId),

            "createdat" => desc ? q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.IngredientId)
                                : q.OrderBy(x => x.CreatedAt).ThenBy(x => x.IngredientId),

            "updatedat" => desc ? q.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.IngredientId)
                                : q.OrderBy(x => x.UpdatedAt).ThenBy(x => x.IngredientId),

            "safetystock" => desc ? q.OrderByDescending(x => x.SafetyStock).ThenByDescending(x => x.IngredientId)
                                  : q.OrderBy(x => x.SafetyStock).ThenBy(x => x.IngredientId),

            "wastethreshold" => desc ? q.OrderByDescending(x => x.WasteThreshold).ThenByDescending(x => x.IngredientId)
                                     : q.OrderBy(x => x.WasteThreshold).ThenBy(x => x.IngredientId),

            _ => desc ? q.OrderByDescending(x => x.Name).ThenByDescending(x => x.IngredientId)
                      : q.OrderBy(x => x.Name).ThenBy(x => x.IngredientId)
        };
    }

    private void RequireAdminOrManager()
    {
        if (!_current.IsInRole(RoleNames.Admin) && !_current.IsInRole(RoleNames.Manager))
            throw new UnauthorizedAccessException("Only Admin/Manager can perform this action.");
    }

    private async Task AddAuditAsync(string action, string entityName, int entityId, object? oldObj, object? newObj, string? reason, CancellationToken ct)
    {
        var log = new AuditLog
        {
            UserId = _current.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldDataJson = oldObj is null ? null : JsonSerializer.Serialize(oldObj),
            NewDataJson = newObj is null ? null : JsonSerializer.Serialize(newObj),
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }

    private static IngredientResponse ToDto(Ingredient x) => new()
    {
        SupplierId = x.SupplierId,
        Id = x.IngredientId,
        SupplierName = x.Supplier?.Name,
        Name = x.Name,
        Unit = x.Unit,
        Status = x.Status,

        Price = x.Price, 

        SafetyStock = x.SafetyStock,
        WasteThreshold = x.WasteThreshold,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(x.UpdatedAt, DateTimeKind.Utc)),
        ShelfLifeDays = x.ShelfLifeDays,
    };
}
