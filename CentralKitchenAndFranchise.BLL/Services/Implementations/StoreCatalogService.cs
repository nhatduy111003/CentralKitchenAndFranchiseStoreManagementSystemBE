using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.StoreCatalog;
using CentralKitchenAndFranchise.DTO.Requests.StoreCatalogs;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.StoreCatalog;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class StoreCatalogService : IStoreCatalogService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFranchiseAccessService _access;

    public StoreCatalogService(AppDbContext db, ICurrentUserService current, IFranchiseAccessService access)
    {
        _db = db;
        _current = current;
        _access = access;
    }

    public async Task<PagedResult<StoreCatalogResponse>> SearchAsync(StoreCatalogListQuery query, CancellationToken ct = default)
    {
        RequireCatalogRead();

        query ??= new StoreCatalogListQuery();

        if (query.FranchiseId <= 0)
            throw new ArgumentException("franchiseId is required and must be a positive integer.");

        await EnsureFranchiseExistsAsync(query.FranchiseId, ct);
        await _access.EnsureCanAccessAsync(query.FranchiseId, ct);

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        if (pageSize > 200) pageSize = 200;

        var status = (query.Status ?? StoreCatalogStatus.Active).Trim().ToUpperInvariant();
        if (status is not (StoreCatalogStatus.Active or StoreCatalogStatus.Inactive or "ALL"))
            throw new ArgumentException("status must be ACTIVE, INACTIVE, or ALL.");

        var productType = query.ProductType?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(productType) && productType is not (ProductTypes.Finished or ProductTypes.SemiFinished))
            throw new ArgumentException("productType must be FINISHED or SEMI_FINISHED.");

        if (query.MinPrice.HasValue && query.MinPrice.Value < 0)
            throw new ArgumentException("minPrice must be >= 0.");

        if (query.MaxPrice.HasValue && query.MaxPrice.Value < 0)
            throw new ArgumentException("maxPrice must be >= 0.");

        if (query.MinPrice.HasValue && query.MaxPrice.HasValue && query.MinPrice > query.MaxPrice)
            throw new ArgumentException("minPrice must be <= maxPrice.");

        var sortBy = (query.SortBy ?? "productName").Trim().ToLowerInvariant();
        var sortDir = (query.SortDir ?? "asc").Trim().ToLowerInvariant();
        if (sortDir is not ("asc" or "desc"))
            throw new ArgumentException("sortDir must be asc or desc.");

        IQueryable<StoreCatalog> q = _db.StoreCatalogs
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Franchise)
            .Where(x => x.FranchiseId == query.FranchiseId);

        if (status != "ALL")
            q = q.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(productType))
            q = q.Where(x => x.Product.ProductType == productType);

        if (query.MinPrice.HasValue)
            q = q.Where(x => x.Price >= query.MinPrice.Value);

        if (query.MaxPrice.HasValue)
            q = q.Where(x => x.Price <= query.MaxPrice.Value);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            q = q.Where(x =>
                EF.Functions.ILike(x.Product.Name, $"%{term}%") ||
                EF.Functions.ILike(x.Product.Sku, $"%{term}%"));
        }

        var total = await q.CountAsync(ct);

        q = ApplySort(q, sortBy, sortDir);

        var entities = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = entities.Select(ToDto).ToList();

        return PagedResult<StoreCatalogResponse>.Create(
            items: items,
            page: page,
            pageSize: pageSize,
            totalItems: total
        );
    }

    public async Task<StoreCatalogResponse> GetByKeyAsync(int franchiseId, int productId, CancellationToken ct = default)
    {
        RequireCatalogRead();

        if (franchiseId <= 0) throw new ArgumentException("franchiseId must be a positive integer.");
        if (productId <= 0) throw new ArgumentException("productId must be a positive integer.");

        await EnsureFranchiseExistsAsync(franchiseId, ct);
        await _access.EnsureCanAccessAsync(franchiseId, ct);

        var entity = await _db.StoreCatalogs
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Franchise)
            .FirstOrDefaultAsync(x => x.FranchiseId == franchiseId && x.ProductId == productId, ct);

        if (entity is null)
            throw new KeyNotFoundException($"StoreCatalog mapping not found (franchiseId={franchiseId}, productId={productId}).");

        return ToDto(entity);
    }

    public async Task<StoreCatalogResponse> AssignAsync(UpsertStoreCatalogRequest request, CancellationToken ct = default)
    {
        RequireCatalogWrite();


        if (request is null) throw new ArgumentException("Request body is required.");
        if (request.FranchiseId <= 0) throw new ArgumentException("franchiseId must be a positive integer.");
        if (request.ProductId <= 0) throw new ArgumentException("productId must be a positive integer.");
        if (request.Price < 0) throw new ArgumentException("price must be >= 0.");

        await EnsureFranchiseExistsAsync(request.FranchiseId, ct);
        await _access.EnsureCanAccessAsync(request.FranchiseId, ct);

        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.ProductId == request.ProductId, ct);
        if (product is null) throw new KeyNotFoundException($"Product {request.ProductId} not found.");
        if (product.Status != ProductStatus.Active)
            throw new InvalidOperationException("Cannot assign an INACTIVE product to store catalog.");

        var now = DateTime.UtcNow;

        var entity = await _db.StoreCatalogs
            .FirstOrDefaultAsync(x => x.FranchiseId == request.FranchiseId && x.ProductId == request.ProductId, ct);

        if (entity is null)
        {
            entity = new StoreCatalog
            {
                FranchiseId = request.FranchiseId,
                ProductId = request.ProductId,
                Price = request.Price,
                Status = StoreCatalogStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.StoreCatalogs.Add(entity);
            await _db.SaveChangesAsync(ct);

            await AddAuditAsync(
                franchiseId: request.FranchiseId,
                action: "STORE_CATALOG_ASSIGN",
                entityName: "StoreCatalog",
                entityId: request.ProductId,
                oldObj: null,
                newObj: new { entity.FranchiseId, entity.ProductId, entity.Price, entity.Status },
                reason: null,
                ct: ct);
        }
        else
        {
            var old = new { entity.Price, entity.Status };

            entity.Price = request.Price;
            entity.Status = StoreCatalogStatus.Active;
            entity.UpdatedAt = now;

            _db.StoreCatalogs.Update(entity);
            await _db.SaveChangesAsync(ct);

            await AddAuditAsync(
                franchiseId: request.FranchiseId,
                action: "STORE_CATALOG_ASSIGN",
                entityName: "StoreCatalog",
                entityId: request.ProductId,
                oldObj: old,
                newObj: new { entity.Price, entity.Status },
                reason: "Upsert/Reactivate",
                ct: ct);
        }

        return await GetByKeyAsync(request.FranchiseId, request.ProductId, ct);
    }

    public async Task<StoreCatalogResponse> UpdateAsync(int franchiseId, int productId, UpdateStoreCatalogRequest request, CancellationToken ct = default)
    {
        RequireCatalogWrite();

        if (franchiseId <= 0) throw new ArgumentException("franchiseId must be a positive integer.");
        if (productId <= 0) throw new ArgumentException("productId must be a positive integer.");
        if (request is null) throw new ArgumentException("Request body is required.");
        if (request.Price < 0) throw new ArgumentException("price must be >= 0.");

        await EnsureFranchiseExistsAsync(franchiseId, ct);
        await _access.EnsureCanAccessAsync(franchiseId, ct);

        var entity = await _db.StoreCatalogs
            .FirstOrDefaultAsync(x => x.FranchiseId == franchiseId && x.ProductId == productId, ct);

        if (entity is null)
            throw new KeyNotFoundException($"StoreCatalog mapping not found (franchiseId={franchiseId}, productId={productId}).");

        var old = new { entity.Price };

        entity.Price = request.Price;
        entity.UpdatedAt = DateTime.UtcNow;

        _db.StoreCatalogs.Update(entity);
        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            franchiseId: franchiseId,
            action: "STORE_CATALOG_UPDATE",
            entityName: "StoreCatalog",
            entityId: productId,
            oldObj: old,
            newObj: new { entity.Price },
            reason: null,
            ct: ct);

        return await GetByKeyAsync(franchiseId, productId, ct);
    }

    public async Task<StoreCatalogResponse> ChangeStatusAsync(int franchiseId, int productId, ChangeStoreCatalogStatusRequest request, CancellationToken ct = default)
    {
        RequireCatalogWrite();

        if (franchiseId <= 0) throw new ArgumentException("franchiseId must be a positive integer.");
        if (productId <= 0) throw new ArgumentException("productId must be a positive integer.");
        if (request is null) throw new ArgumentException("Request body is required.");

        await EnsureFranchiseExistsAsync(franchiseId, ct);
        await _access.EnsureCanAccessAsync(franchiseId, ct);

        var newStatus = request.Status?.Trim().ToUpperInvariant();
        if (newStatus is not (StoreCatalogStatus.Active or StoreCatalogStatus.Inactive))
            throw new ArgumentException("status must be ACTIVE or INACTIVE.");

        var entity = await _db.StoreCatalogs
            .FirstOrDefaultAsync(x => x.FranchiseId == franchiseId && x.ProductId == productId, ct);

        if (entity is null)
            throw new KeyNotFoundException($"StoreCatalog mapping not found (franchiseId={franchiseId}, productId={productId}).");

        var old = new { entity.Status };

        entity.Status = newStatus;
        entity.UpdatedAt = DateTime.UtcNow;

        _db.StoreCatalogs.Update(entity);
        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            franchiseId: franchiseId,
            action: "STORE_CATALOG_STATUS_CHANGE",
            entityName: "StoreCatalog",
            entityId: productId,
            oldObj: old,
            newObj: new { entity.Status },
            reason: string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            ct: ct);

        return await GetByKeyAsync(franchiseId, productId, ct);
    }

    public async Task DeleteAsync(int franchiseId, int productId, CancellationToken ct = default)
    {
        RequireCatalogWrite();

        await ChangeStatusAsync(franchiseId, productId, new ChangeStoreCatalogStatusRequest
        {
            Status = StoreCatalogStatus.Inactive,
            Reason = "Deactivated via DELETE endpoint"
        }, ct);
    }

    private void RequireCatalogWrite()
    {
        var role = _current.Role;
        if (role != RoleNames.Admin && role != RoleNames.Manager)
            throw new UnauthorizedAccessException("Only Admin/Manager can perform this action.");
    }

    private void RequireCatalogRead()
    {
        var role = _current.Role;
        if (role != RoleNames.KitchenStaff && role != RoleNames.SupplyCoordinator)
            throw new UnauthorizedAccessException("Only Admin/Manager/StoreStaff can perform this action.");
    }

    private async Task EnsureFranchiseExistsAsync(int franchiseId, CancellationToken ct)
    {
        var exists = await _db.Franchises.AsNoTracking().AnyAsync(x => x.FranchiseId == franchiseId, ct);
        if (!exists) throw new KeyNotFoundException($"Franchise {franchiseId} not found.");
    }

    private async Task AddAuditAsync(
        int franchiseId,
        string action,
        string entityName,
        int entityId,
        object? oldObj,
        object? newObj,
        string? reason,
        CancellationToken ct)
    {
        var log = new AuditLog
        {
            UserId = _current.UserId,
            FranchiseId = franchiseId,
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

    private static IQueryable<StoreCatalog> ApplySort(IQueryable<StoreCatalog> q, string sortBy, string sortDir)
    {
        var desc = sortDir == "desc";

        return sortBy switch
        {
            "productid" => desc
                ? q.OrderByDescending(x => x.ProductId).ThenByDescending(x => x.FranchiseId)
                : q.OrderBy(x => x.ProductId).ThenBy(x => x.FranchiseId),

            "sku" => desc
                ? q.OrderByDescending(x => x.Product.Sku).ThenByDescending(x => x.ProductId)
                : q.OrderBy(x => x.Product.Sku).ThenBy(x => x.ProductId),

            "price" => desc
                ? q.OrderByDescending(x => x.Price).ThenByDescending(x => x.ProductId)
                : q.OrderBy(x => x.Price).ThenBy(x => x.ProductId),

            "status" => desc
                ? q.OrderByDescending(x => x.Status).ThenByDescending(x => x.ProductId)
                : q.OrderBy(x => x.Status).ThenBy(x => x.ProductId),

            "createdat" => desc
                ? q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.ProductId)
                : q.OrderBy(x => x.CreatedAt).ThenBy(x => x.ProductId),

            "updatedat" => desc
                ? q.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.ProductId)
                : q.OrderBy(x => x.UpdatedAt).ThenBy(x => x.ProductId),

            "productname" or _ => desc
                ? q.OrderByDescending(x => x.Product.Name).ThenByDescending(x => x.ProductId)
                : q.OrderBy(x => x.Product.Name).ThenBy(x => x.ProductId),
        };
    }

    private static StoreCatalogResponse ToDto(StoreCatalog x) => new()
    {
        FranchiseId = x.FranchiseId,
        FranchiseName = x.Franchise.Name,

        ProductId = x.ProductId,
        ProductName = x.Product.Name,
        Sku = x.Product.Sku,
        Unit = x.Product.Unit,
        ProductType = x.Product.ProductType,

        Price = x.Price,
        Status = x.Status,

        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc)),
        UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(x.UpdatedAt, DateTimeKind.Utc))
    };
}
