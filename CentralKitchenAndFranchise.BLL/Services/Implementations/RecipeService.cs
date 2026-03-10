using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Recipes;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Recipes;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class RecipeService : IRecipeService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public RecipeService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PagedResult<RecipeResponse>> SearchAsync(RecipeListQuery query, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        query ??= new RecipeListQuery();

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

        IQueryable<Recipe> q = _db.Recipes.AsNoTracking();

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

        return PagedResult<RecipeResponse>.Create(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<RecipeResponse> GetByIdAsync(int id, CancellationToken ct = default)
    {
        RequireAdminOrManager();

        var entity = await _db.Recipes.AsNoTracking().FirstOrDefaultAsync(x => x.RecipeId == id, ct);
        if (entity is null) throw new KeyNotFoundException($"Recipe {id} not found.");
        return ToDto(entity);
    }

    public async Task<RecipeResponse> CreateAsync(CreateRecipeRequest request, CancellationToken ct = default)
    {
        RequireAdminOrManager();

        if (request is null) throw new ArgumentException("Request body is required.");
        if (request.ProductId <= 0) throw new ArgumentException("ProductId is required.");

        var productExists = await _db.Products.AnyAsync(x => x.ProductId == request.ProductId, ct);
        if (!productExists) throw new KeyNotFoundException($"Product {request.ProductId} not found.");

        var currentMaxVersion = await _db.Recipes
            .Where(x => x.ProductId == request.ProductId)
            .Select(x => (int?)x.Version)
            .MaxAsync(ct) ?? 0;

        var entity = new Recipe
        {
            ProductId = request.ProductId,
            Version = currentMaxVersion + 1,
            Status = StandardizationStatuses.Draft,
            Instructions = string.IsNullOrWhiteSpace(request.Instructions) ? null : request.Instructions.Trim()
        };

        _db.Recipes.Add(entity);
        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "RECIPE_CREATE",
            entityName: "Recipe",
            entityId: entity.RecipeId,
            oldObj: null,
            newObj: entity,
            reason: null,
            ct: ct);

        return await GetByIdAsync(entity.RecipeId, ct);
    }

    public async Task<RecipeResponse> UpdateAsync(int id, UpdateRecipeRequest request, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        if (request is null) throw new ArgumentException("Request body is required.");

        var entity = await _db.Recipes.FirstOrDefaultAsync(x => x.RecipeId == id, ct);
        if (entity is null) throw new KeyNotFoundException($"Recipe {id} not found.");

        if (entity.Status == StandardizationStatuses.Active)
            throw new InvalidOperationException("Cannot update an ACTIVE Recipe. Create a new version instead.");

        var old = new { entity.Instructions, entity.Status };

        entity.Instructions = string.IsNullOrWhiteSpace(request.Instructions) ? null : request.Instructions.Trim();

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "RECIPE_UPDATE",
            entityName: "Recipe",
            entityId: entity.RecipeId,
            oldObj: old,
            newObj: new { entity.Instructions, entity.Status },
            reason: null,
            ct: ct);

        return await GetByIdAsync(entity.RecipeId, ct);
    }

    public async Task<RecipeResponse> ChangeStatusAsync(int id, ChangeRecipeStatusRequest request, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        if (request is null) throw new ArgumentException("Request body is required.");

        var newStatus = (request.Status ?? "").Trim().ToUpperInvariant();
        if (!StandardizationStatuses.IsValid(newStatus))
            throw new ArgumentException("Status must be DRAFT, ACTIVE, or INACTIVE.");

        var entity = await _db.Recipes.FirstOrDefaultAsync(x => x.RecipeId == id, ct);
        if (entity is null) throw new KeyNotFoundException($"Recipe {id} not found.");

        if (newStatus == StandardizationStatuses.Active)
        {
            var hasOtherActive = await _db.Recipes.AnyAsync(x =>
                x.ProductId == entity.ProductId &&
                x.RecipeId != entity.RecipeId &&
                x.Status == StandardizationStatuses.Active, ct);

            if (hasOtherActive)
                throw new InvalidOperationException("Another ACTIVE Recipe already exists for this product.");
        }

        var old = new { entity.Status };
        entity.Status = newStatus;

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "RECIPE_STATUS_CHANGE",
            entityName: "Recipe",
            entityId: entity.RecipeId,
            oldObj: old,
            newObj: new { entity.Status },
            reason: request.Reason,
            ct: ct);

        return await GetByIdAsync(entity.RecipeId, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        RequireAdminOrManager();

        var entity = await _db.Recipes.FirstOrDefaultAsync(x => x.RecipeId == id, ct);
        if (entity is null) throw new KeyNotFoundException($"Recipe {id} not found.");

        var old = new { entity.Status };
        entity.Status = StandardizationStatuses.Inactive;

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "RECIPE_DELETE",
            entityName: "Recipe",
            entityId: entity.RecipeId,
            oldObj: old,
            newObj: new { entity.Status },
            reason: "Soft delete => INACTIVE",
            ct: ct);
    }

    private static IQueryable<Recipe> ApplySort(IQueryable<Recipe> q, string sortBy, string sortDir)
    {
        var desc = sortDir == "desc";
        return sortBy switch
        {
            "productid" => desc ? q.OrderByDescending(x => x.ProductId).ThenByDescending(x => x.Version) : q.OrderBy(x => x.ProductId).ThenBy(x => x.Version),
            "version" => desc ? q.OrderByDescending(x => x.Version).ThenByDescending(x => x.RecipeId) : q.OrderBy(x => x.Version).ThenBy(x => x.RecipeId),
            "status" => desc ? q.OrderByDescending(x => x.Status).ThenByDescending(x => x.RecipeId) : q.OrderBy(x => x.Status).ThenBy(x => x.RecipeId),
            "createdat" => desc ? q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.RecipeId) : q.OrderBy(x => x.CreatedAt).ThenBy(x => x.RecipeId),
            "updatedat" => desc ? q.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.RecipeId) : q.OrderBy(x => x.UpdatedAt).ThenBy(x => x.RecipeId),
            "id" or _ => desc ? q.OrderByDescending(x => x.RecipeId) : q.OrderBy(x => x.RecipeId),
        };
    }

    private static RecipeResponse ToDto(Recipe x) => new()
    {
        Id = x.RecipeId,
        ProductId = x.ProductId,
        Version = x.Version,
        Status = x.Status,
        Instructions = x.Instructions,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
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
            OldDataJson = oldObj is null ? null : JsonSerializer.Serialize(oldObj),
            NewDataJson = newObj is null ? null : JsonSerializer.Serialize(newObj),
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }
}