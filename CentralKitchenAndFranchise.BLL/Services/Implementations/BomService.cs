using System.Text.Json;
using System.Text.Json.Serialization;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Boms;
using CentralKitchenAndFranchise.DTO.Responses.Boms;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class BomService : IBomService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    // Extra safety: even if a service accidentally passes an EF graph to audit,
    // we won't throw "object cycle" exceptions.
    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public BomService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PagedResult<BomResponse>> SearchAsync(BomListQuery query, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        query ??= new BomListQuery();

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        if (pageSize > 200) pageSize = 200;

        var status = (query.Status ?? "ALL").Trim().ToUpperInvariant();
        if (status != "ALL" && !StandardizationStatuses.IsValid(status))
            throw new ArgumentException("status must be DRAFT, ACTIVE, INACTIVE, or ALL.");

        var sortBy = (query.SortBy ?? "id").Trim().ToLowerInvariant();
        var sortDir = (query.SortDir ?? "desc").Trim().ToLowerInvariant();
        if (sortDir is not ("asc" or "desc"))
            throw new ArgumentException("sortDir must be asc or desc.");

        IQueryable<Bom> q = _db.Boms
            .AsNoTracking()
            .Include(x => x.Items)
            .ThenInclude(i => i.Ingredient);

        if (query.ProductId is > 0)
            q = q.Where(x => x.ProductId == query.ProductId.Value);

        if (status != "ALL")
            q = q.Where(x => x.Status == status);

        var total = await q.CountAsync(ct);

        q = ApplySort(q, sortBy, sortDir);

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<BomResponse>.Create(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<BomResponse> GetByIdAsync(int id, CancellationToken ct = default)
    {
        RequireAdminOrManager();

        var entity = await _db.Boms
            .AsNoTracking()
            .Include(x => x.Items)
            .ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(x => x.BomId == id, ct);

        if (entity is null) throw new KeyNotFoundException($"BOM {id} not found.");
        return ToDto(entity);
    }

    public async Task<BomResponse> CreateAsync(CreateBomRequest request, CancellationToken ct = default)
    {
        RequireAdminOrManager();

        if (request is null) throw new ArgumentException("Request body is required.");
        if (request.ProductId <= 0) throw new ArgumentException("ProductId is required.");

        var productExists = await _db.Products.AnyAsync(x => x.ProductId == request.ProductId, ct);
        if (!productExists) throw new KeyNotFoundException($"Product {request.ProductId} not found.");

        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("Items is required (at least 1 ingredient).");

        var ingredientIds = request.Items.Select(x => x.IngredientId).Distinct().ToList();
        if (ingredientIds.Any(id => id <= 0))
            throw new ArgumentException("IngredientId must be > 0.");

        if (request.Items.Any(x => x.Quantity <= 0))
            throw new ArgumentException("Quantity must be > 0.");

        var existingIngredients = await _db.Ingredients
            .Where(x => ingredientIds.Contains(x.IngredientId) && x.Status == "ACTIVE")
            .Select(x => x.IngredientId)
            .ToListAsync(ct);

        var missing = ingredientIds.Except(existingIngredients).ToList();
        if (missing.Count > 0)
            throw new KeyNotFoundException($"Ingredient(s) not found or inactive: {string.Join(", ", missing)}");

        var currentMaxVersion = await _db.Boms
            .Where(x => x.ProductId == request.ProductId)
            .Select(x => (int?)x.Version)
            .MaxAsync(ct) ?? 0;

        var entity = new Bom
        {
            ProductId = request.ProductId,
            Version = currentMaxVersion + 1,
            Status = StandardizationStatuses.Draft,
            Items = request.Items
                .GroupBy(x => x.IngredientId)
                .Select(g => new BomItem
                {
                    IngredientId = g.Key,
                    Quantity = g.Sum(v => v.Quantity)
                })
                .ToList()
        };

        _db.Boms.Add(entity);
        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "BOM_CREATE",
            entityName: "Bom",
            entityId: entity.BomId,
            oldObj: null,
            newObj: BuildBomSnapshot(entity),
            reason: null,
            ct: ct);

        return await GetByIdAsync(entity.BomId, ct);
    }

    public async Task<BomResponse> UpdateAsync(int id, UpdateBomRequest request, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        if (request is null) throw new ArgumentException("Request body is required.");

        var entity = await _db.Boms
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.BomId == id, ct);

        if (entity is null) throw new KeyNotFoundException($"BOM {id} not found.");

        if (entity.Status == StandardizationStatuses.Active)
            throw new InvalidOperationException("Cannot update an ACTIVE BOM. Create a new version instead.");

        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("Items is required (at least 1 ingredient).");

        if (request.Items.Any(x => x.IngredientId <= 0 || x.Quantity <= 0))
            throw new ArgumentException("Each item requires IngredientId > 0 and Quantity > 0.");

        var ingredientIds = request.Items.Select(x => x.IngredientId).Distinct().ToList();
        var existingIngredients = await _db.Ingredients
            .Where(x => ingredientIds.Contains(x.IngredientId) && x.Status == "ACTIVE")
            .Select(x => x.IngredientId)
            .ToListAsync(ct);

        var missing = ingredientIds.Except(existingIngredients).ToList();
        if (missing.Count > 0)
            throw new KeyNotFoundException($"Ingredient(s) not found or inactive: {string.Join(", ", missing)}");

        var old = new
        {
            entity.ProductId,
            entity.Version,
            entity.Status,
            Items = entity.Items.Select(i => new { i.IngredientId, i.Quantity }).ToList()
        };

        _db.BomItems.RemoveRange(entity.Items);
        entity.Items = request.Items
            .GroupBy(x => x.IngredientId)
            .Select(g => new BomItem
            {
                IngredientId = g.Key,
                Quantity = g.Sum(v => v.Quantity)
            })
            .ToList();

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "BOM_UPDATE",
            entityName: "Bom",
            entityId: entity.BomId,
            oldObj: old,
            newObj: BuildBomSnapshot(entity),
            reason: null,
            ct: ct);

        return await GetByIdAsync(entity.BomId, ct);
    }

    public async Task<BomResponse> ChangeStatusAsync(int id, ChangeBomStatusRequest request, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        if (request is null) throw new ArgumentException("Request body is required.");

        var newStatus = (request.Status ?? "").Trim().ToUpperInvariant();
        if (!StandardizationStatuses.IsValid(newStatus))
            throw new ArgumentException("Status must be DRAFT, ACTIVE, or INACTIVE.");

        var entity = await _db.Boms.FirstOrDefaultAsync(x => x.BomId == id, ct);
        if (entity is null) throw new KeyNotFoundException($"BOM {id} not found.");

        if (newStatus == StandardizationStatuses.Active)
        {
            var hasOtherActive = await _db.Boms.AnyAsync(x =>
                x.ProductId == entity.ProductId &&
                x.BomId != entity.BomId &&
                x.Status == StandardizationStatuses.Active, ct);

            if (hasOtherActive)
                throw new InvalidOperationException("Another ACTIVE BOM already exists for this product.");
        }

        var old = new { entity.Status };
        entity.Status = newStatus;

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "BOM_STATUS_CHANGE",
            entityName: "Bom",
            entityId: entity.BomId,
            oldObj: old,
            newObj: new { entity.Status },
            reason: request.Reason,
            ct: ct);

        return await GetByIdAsync(entity.BomId, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        RequireAdminOrManager();

        var entity = await _db.Boms.FirstOrDefaultAsync(x => x.BomId == id, ct);
        if (entity is null) throw new KeyNotFoundException($"BOM {id} not found.");

        var old = new { entity.Status };
        entity.Status = StandardizationStatuses.Inactive;

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "BOM_DELETE",
            entityName: "Bom",
            entityId: entity.BomId,
            oldObj: old,
            newObj: new { entity.Status },
            reason: "Soft delete => INACTIVE",
            ct: ct);
    }

    private static IQueryable<Bom> ApplySort(IQueryable<Bom> q, string sortBy, string sortDir)
    {
        var desc = sortDir == "desc";
        return sortBy switch
        {
            "productid" => desc ? q.OrderByDescending(x => x.ProductId).ThenByDescending(x => x.Version) : q.OrderBy(x => x.ProductId).ThenBy(x => x.Version),
            "version" => desc ? q.OrderByDescending(x => x.Version).ThenByDescending(x => x.BomId) : q.OrderBy(x => x.Version).ThenBy(x => x.BomId),
            "status" => desc ? q.OrderByDescending(x => x.Status).ThenByDescending(x => x.BomId) : q.OrderBy(x => x.Status).ThenBy(x => x.BomId),
            "createdat" => desc ? q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.BomId) : q.OrderBy(x => x.CreatedAt).ThenBy(x => x.BomId),
            "updatedat" => desc ? q.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.BomId) : q.OrderBy(x => x.UpdatedAt).ThenBy(x => x.BomId),
            "id" or _ => desc ? q.OrderByDescending(x => x.BomId) : q.OrderBy(x => x.BomId),
        };
    }

    private static BomResponse ToDto(Bom x) => new()
    {
        Id = x.BomId,
        ProductId = x.ProductId,
        Version = x.Version,
        Status = x.Status,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt,
        Items = x.Items
            .OrderBy(i => i.IngredientId)
            .Select(i => new BomItemResponse
            {
                IngredientId = i.IngredientId,
                IngredientName = i.Ingredient?.Name ?? "",
                IngredientUnit = i.Ingredient?.Unit ?? "",
                Quantity = i.Quantity
            })
            .ToList()
    };

    private void RequireAdminOrManager()
    {
        if (_current.Role is not (RoleNames.Admin or RoleNames.Manager))
            throw new UnauthorizedAccessException("Forbidden.");
    }

    private async Task AddAuditAsync(string action, string entityName, int entityId, object? oldObj, object? newObj, string? reason, CancellationToken ct)
    {
        var log = new AuditLog
        {
            UserId = _current.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldDataJson = oldObj is null ? null : JsonSerializer.Serialize(oldObj, AuditJsonOptions),
            NewDataJson = newObj is null ? null : JsonSerializer.Serialize(newObj, AuditJsonOptions),
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }

    private static object BuildBomSnapshot(Bom entity)
    {
        // IMPORTANT: Do NOT serialize EF entities directly. They can contain navigation cycles
        // (e.g., Bom -> Items -> BomItem -> Bom), causing runtime 500.
        return new
        {
            entity.BomId,
            entity.ProductId,
            entity.Version,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt,
            Items = entity.Items
                .OrderBy(i => i.IngredientId)
                .Select(i => new { i.IngredientId, i.Quantity })
                .ToList()
        };
    }
}