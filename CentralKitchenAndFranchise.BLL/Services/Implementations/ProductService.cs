using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Products;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Products;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class ProductService : IProductService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public ProductService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    // =========================
    // READ
    // =========================
    public async Task<PagedResult<ProductResponse>> SearchAsync(ProductListQuery query, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        query ??= new ProductListQuery();

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        if (pageSize > 200) pageSize = 200;

        var status = (query.Status ?? ProductStatus.Active).Trim().ToUpperInvariant();
        if (status is not (ProductStatus.Active or ProductStatus.Inactive or "ALL"))
            throw new ArgumentException("status must be ACTIVE, INACTIVE, or ALL.");

        var productType = query.ProductType?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(productType) && productType is not (ProductTypes.Finished or ProductTypes.SemiFinished))
            throw new ArgumentException("productType must be FINISHED or SEMI_FINISHED.");

        var sortBy = (query.SortBy ?? "name").Trim().ToLowerInvariant();
        var sortDir = (query.SortDir ?? "asc").Trim().ToLowerInvariant();
        if (sortDir is not ("asc" or "desc"))
            throw new ArgumentException("sortDir must be asc or desc.");

        IQueryable<Product> q = _db.Products.AsNoTracking();

        if (status != "ALL")
            q = q.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(productType))
            q = q.Where(x => x.ProductType == productType);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            q = q.Where(x =>
                EF.Functions.ILike(x.Name, $"%{term}%") ||
                EF.Functions.ILike(x.Sku, $"%{term}%"));
        }

        var total = await q.CountAsync(ct);

        q = ApplySort(q, sortBy, sortDir);

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ProductResponse
            {
                Id = x.ProductId,
                Name = x.Name,
                Sku = x.Sku,
                Unit = x.Unit,
                Status = x.Status,
                ProductType = x.ProductType
            })
            .ToListAsync(ct);

        return PagedResult<ProductResponse>.Create(items, page, pageSize, total);
    }

    public async Task<ProductResponse> GetByIdAsync(int id, CancellationToken ct = default)
    {
        RequireAdminOrManager();

        if (id <= 0) throw new ArgumentException("id must be a positive integer.");

        var p = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.ProductId == id, ct);
        if (p is null) throw new KeyNotFoundException($"Product {id} not found.");

        return new ProductResponse
        {
            Id = p.ProductId,
            Name = p.Name,
            Sku = p.Sku,
            Unit = p.Unit,
            Status = p.Status,
            ProductType = p.ProductType
        };
    }

    // =========================
    // WRITE (CRUD)
    // =========================
    public async Task<int> CreateAsync(ProductCreateRequest req, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        req = req ?? throw new ArgumentNullException(nameof(req));

        var name = req.Name.Trim();
        var sku = req.Sku.Trim();
        var unit = req.Unit.Trim();
        var type = NormalizeProductType(req.ProductType);

        // soft conflict guard (no DB unique index currently)
        var existsSku = await _db.Products.AsNoTracking().AnyAsync(x => x.Sku == sku, ct);
        if (existsSku) throw new InvalidOperationException($"SKU '{sku}' already exists.");

        var entity = new Product
        {
            Name = name,
            Sku = sku,
            Unit = unit,
            ProductType = type,
            Status = ProductStatus.Active
        };

        await _db.Products.AddAsync(entity, ct);

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            Action = "CREATE",
            EntityName = nameof(Product),
            EntityId = null,
            NewDataJson = JsonSerializer.Serialize(new
            {
                entity.ProductId,
                entity.Name,
                entity.Sku,
                entity.Unit,
                entity.ProductType,
                entity.Status
            }),
            Reason = "Create product master data",
            CreatedAt = DateTime.UtcNow
        }, ct);

        await _db.SaveChangesAsync(ct);
        return entity.ProductId;
    }

    public async Task UpdateAsync(int id, ProductUpdateRequest req, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        if (id <= 0) throw new ArgumentException("id must be a positive integer.");
        req = req ?? throw new ArgumentNullException(nameof(req));

        var entity = await _db.Products.FirstOrDefaultAsync(x => x.ProductId == id, ct);
        if (entity is null) throw new KeyNotFoundException($"Product {id} not found.");

        var name = req.Name.Trim();
        var sku = req.Sku.Trim();
        var unit = req.Unit.Trim();
        var type = NormalizeProductType(req.ProductType);

        var existsSku = await _db.Products.AsNoTracking()
            .AnyAsync(x => x.Sku == sku && x.ProductId != id, ct);
        if (existsSku) throw new InvalidOperationException($"SKU '{sku}' already exists.");

        var old = new
        {
            entity.Name,
            entity.Sku,
            entity.Unit,
            entity.ProductType,
            entity.Status
        };

        entity.Name = name;
        entity.Sku = sku;
        entity.Unit = unit;
        entity.ProductType = type;

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            Action = "UPDATE",
            EntityName = nameof(Product),
            EntityId = entity.ProductId,
            OldDataJson = JsonSerializer.Serialize(old),
            NewDataJson = JsonSerializer.Serialize(new
            {
                entity.Name,
                entity.Sku,
                entity.Unit,
                entity.ProductType,
                entity.Status
            }),
            Reason = "Update product master data",
            CreatedAt = DateTime.UtcNow
        }, ct);

        await _db.SaveChangesAsync(ct);
    }

    public async Task ChangeStatusAsync(int id, ProductStatusUpdateRequest req, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        if (id <= 0) throw new ArgumentException("id must be a positive integer.");
        req = req ?? throw new ArgumentNullException(nameof(req));

        var status = (req.Status ?? "").Trim().ToUpperInvariant();
        if (status is not (ProductStatus.Active or ProductStatus.Inactive))
            throw new ArgumentException("status must be ACTIVE or INACTIVE.");

        var entity = await _db.Products.FirstOrDefaultAsync(x => x.ProductId == id, ct);
        if (entity is null) throw new KeyNotFoundException($"Product {id} not found.");

        var old = new { entity.Status };

        entity.Status = status;

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            Action = "CHANGE_STATUS",
            EntityName = nameof(Product),
            EntityId = entity.ProductId,
            OldDataJson = JsonSerializer.Serialize(old),
            NewDataJson = JsonSerializer.Serialize(new { entity.Status }),
            Reason = "Change product status",
            CreatedAt = DateTime.UtcNow
        }, ct);

        await _db.SaveChangesAsync(ct);
    }

    public Task DeactivateAsync(int id, CancellationToken ct = default)
        => ChangeStatusAsync(id, new ProductStatusUpdateRequest { Status = ProductStatus.Inactive }, ct);

    // =========================
    // Helpers
    // =========================
    private void RequireAdminOrManager()
    {
        var role = _current.Role;
        if (role != RoleNames.Admin && role != RoleNames.Manager)
            throw new UnauthorizedAccessException("Only Admin/Manager can perform this action.");
    }

    private static string NormalizeProductType(string productType)
    {
        var t = (productType ?? "").Trim().ToUpperInvariant();
        if (t is not (ProductTypes.Finished or ProductTypes.SemiFinished))
            throw new ArgumentException("productType must be FINISHED or SEMI_FINISHED.");
        return t;
    }

    private static IQueryable<Product> ApplySort(IQueryable<Product> q, string sortBy, string sortDir)
    {
        var desc = sortDir == "desc";

        return sortBy switch
        {
            "id" => desc ? q.OrderByDescending(x => x.ProductId) : q.OrderBy(x => x.ProductId),
            "sku" => desc ? q.OrderByDescending(x => x.Sku) : q.OrderBy(x => x.Sku),
            "unit" => desc ? q.OrderByDescending(x => x.Unit) : q.OrderBy(x => x.Unit),
            "status" => desc ? q.OrderByDescending(x => x.Status) : q.OrderBy(x => x.Status),
            "producttype" => desc ? q.OrderByDescending(x => x.ProductType) : q.OrderBy(x => x.ProductType),
            "name" or _ => desc ? q.OrderByDescending(x => x.Name) : q.OrderBy(x => x.Name),
        };
    }
}