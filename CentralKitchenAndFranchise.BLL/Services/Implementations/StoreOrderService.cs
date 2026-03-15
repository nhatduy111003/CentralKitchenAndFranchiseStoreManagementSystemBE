using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.StoreOrders;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.StoreOrders;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class StoreOrderService : IStoreOrderService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFranchiseAccessService _access;

    public StoreOrderService(AppDbContext db, ICurrentUserService current, IFranchiseAccessService access)
    {
        _db = db;
        _current = current;
        _access = access;
    }

    public async Task<StoreOrderResponse> CreateAsync(int franchiseId, CreateStoreOrderRequest request, CancellationToken ct = default)
    {
        RequireRoles(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff); 
        await _access.EnsureCanAccessAsync(franchiseId, ct);

        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("Items are required.");

        await EnforceFutureOrderLimitAsync(request.OrderDate, ct);

        // validate items: qty > 0 + product assigned in store catalog
        var itemMap = request.Items
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        foreach (var (productId, qty) in itemMap)
        {
            if (productId <= 0) throw new ArgumentException("ProductId must be positive.");
            if (qty <= 0) throw new ArgumentException("Quantity must be > 0.");
        }

        var allowedProductIds = await _db.StoreCatalogs
            .AsNoTracking()
            .Where(x => x.FranchiseId == franchiseId && x.Status == "ACTIVE")
            .Select(x => x.ProductId)
            .ToListAsync(ct);

        var invalid = itemMap.Keys.Where(pid => !allowedProductIds.Contains(pid)).ToList();
        if (invalid.Count > 0)
            throw new ArgumentException("Order contains products not assigned to store catalog (FR-027).");

        var now = DateTime.UtcNow;

        var order = new StoreOrder
        {
            FranchiseId = franchiseId,
            Status = StoreOrderStatus.Draft,
            OrderDate = request.OrderDate,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.StoreOrders.Add(order);
        await _db.SaveChangesAsync(ct);

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => itemMap.Keys.Contains(p.ProductId) && p.Status == "ACTIVE")
            .Select(p => new { p.ProductId })
            .ToListAsync(ct);

        var existingActive = products.Select(x => x.ProductId).ToHashSet();
        var missing = itemMap.Keys.Where(pid => !existingActive.Contains(pid)).ToList();
        if (missing.Count > 0)
            throw new ArgumentException("Some products are not ACTIVE or not found.");

        foreach (var (productId, qty) in itemMap)
        {
            _db.StoreOrderItems.Add(new StoreOrderItem
            {
                StoreOrderId = order.StoreOrderId,
                ProductId = productId,
                Quantity = qty
            });
        }

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "STORE_ORDER_CREATE",
            franchiseId: franchiseId,
            entityName: "StoreOrder",
            entityId: order.StoreOrderId,
            oldObj: null,
            newObj: new { order.StoreOrderId, order.Status, order.OrderDate, Items = itemMap },
            reason: null,
            ct: ct);

        return await GetByIdInternalAsync(franchiseId, order.StoreOrderId, ct);
    }

    public async Task<StoreOrderResponse> UpdateAsync(int franchiseId, int orderId, UpdateStoreOrderRequest request, CancellationToken ct = default)
    {
        RequireRoles(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff); 
        await _access.EnsureCanAccessAsync(franchiseId, ct);

        var order = await _db.StoreOrders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.StoreOrderId == orderId && x.FranchiseId == franchiseId, ct);

        if (order is null) throw new KeyNotFoundException($"StoreOrder {orderId} not found.");

        await EnsureCanEditAsync(order, ct);
        await EnforceFutureOrderLimitAsync(request.OrderDate, ct);

        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("Items are required.");

        var itemMap = request.Items
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        foreach (var (productId, qty) in itemMap)
        {
            if (productId <= 0) throw new ArgumentException("ProductId must be positive.");
            if (qty <= 0) throw new ArgumentException("Quantity must be > 0.");
        }

        var allowedProductIds = await _db.StoreCatalogs
            .AsNoTracking()
            .Where(x => x.FranchiseId == franchiseId && x.Status == "ACTIVE")
            .Select(x => x.ProductId)
            .ToListAsync(ct);

        var invalid = itemMap.Keys.Where(pid => !allowedProductIds.Contains(pid)).ToList();
        if (invalid.Count > 0)
            throw new ArgumentException("Order contains products not assigned to store catalog (FR-027).");

        var old = new
        {
            order.Status,
            order.OrderDate,
            Items = order.Items.Select(i => new { i.ProductId, i.Quantity }).ToList()
        };

        order.OrderDate = request.OrderDate;
        order.UpdatedAt = DateTime.UtcNow;

        // replace items (simple + consistent for week6)
        _db.StoreOrderItems.RemoveRange(order.Items);
        await _db.SaveChangesAsync(ct);

        foreach (var (productId, qty) in itemMap)
        {
            _db.StoreOrderItems.Add(new StoreOrderItem
            {
                StoreOrderId = order.StoreOrderId,
                ProductId = productId,
                Quantity = qty
            });
        }

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "STORE_ORDER_UPDATE",
            franchiseId: franchiseId,
            entityName: "StoreOrder",
            entityId: order.StoreOrderId,
            oldObj: old,
            newObj: new { order.Status, order.OrderDate, Items = itemMap },
            reason: null,
            ct: ct);

        return await GetByIdInternalAsync(franchiseId, order.StoreOrderId, ct);
    }

    public async Task<StoreOrderResponse> SubmitAsync(int franchiseId, int orderId, CancellationToken ct = default)
    {
        RequireRoles(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff);
        await _access.EnsureCanAccessAsync(franchiseId, ct);

        var order = await _db.StoreOrders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.StoreOrderId == orderId && x.FranchiseId == franchiseId, ct);

        if (order is null) throw new KeyNotFoundException($"StoreOrder {orderId} not found.");

        if (order.Status != StoreOrderStatus.Draft)
            throw new InvalidOperationException("Only DRAFT orders can be submitted.");

        if (order.Items.Count == 0)
            throw new ArgumentException("Cannot submit an order with no items.");

        var minutes = await GetIntSettingAsync(SettingKeys.OrderEditWindowMinutes, fallback: 30, ct);
        var now = DateTime.UtcNow;

        order.Status = StoreOrderStatus.Submitted;
        order.SubmittedAt = now;
        order.LockedAt = now.AddMinutes(minutes);
        order.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "STORE_ORDER_SUBMIT",
            franchiseId: franchiseId,
            entityName: "StoreOrder",
            entityId: order.StoreOrderId,
            oldObj: new { Status = StoreOrderStatus.Draft },
            newObj: new { order.Status, order.SubmittedAt, order.LockedAt },
            reason: null,
            ct: ct);

        return await GetByIdInternalAsync(franchiseId, order.StoreOrderId, ct);
    }

    public async Task<StoreOrderResponse> CancelAsync(int franchiseId, int orderId, string? reason, CancellationToken ct = default)
    {
        RequireRoles(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff);
        await _access.EnsureCanAccessAsync(franchiseId, ct);

        var order = await _db.StoreOrders
            .FirstOrDefaultAsync(x => x.StoreOrderId == orderId && x.FranchiseId == franchiseId, ct);

        if (order is null) throw new KeyNotFoundException($"StoreOrder {orderId} not found.");

        // cancel allowed when not locked and not cancelled
        if (order.Status == StoreOrderStatus.Cancelled)
            throw new InvalidOperationException("Order is already cancelled.");

        if (order.LockedAt.HasValue && DateTime.UtcNow >= order.LockedAt.Value)
            throw new InvalidOperationException("Order is locked. Cancel is not allowed (FR-039).");

        var old = new { order.Status, order.CancelledAt, order.CancelReason };

        order.Status = StoreOrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "STORE_ORDER_CANCEL",
            franchiseId: franchiseId,
            entityName: "StoreOrder",
            entityId: order.StoreOrderId,
            oldObj: old,
            newObj: new { order.Status, order.CancelledAt, order.CancelReason },
            reason: order.CancelReason,
            ct: ct);

        return await GetByIdInternalAsync(franchiseId, order.StoreOrderId, ct);
    }

    public async Task<PagedResult<StoreOrderResponse>> SearchAsync(int franchiseId, StoreOrderListQuery query, CancellationToken ct = default)
    {
        RequireRoles(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff); 
        await _access.EnsureCanAccessAsync(franchiseId, ct);

        query ??= new StoreOrderListQuery();

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        if (pageSize > 200) pageSize = 200;

        var status = (query.Status ?? "ALL").Trim().ToUpperInvariant();
        if (status is not ("ALL" or StoreOrderStatus.Draft or StoreOrderStatus.Submitted or StoreOrderStatus.Locked or StoreOrderStatus.Cancelled))
            throw new ArgumentException("status must be DRAFT, SUBMITTED, LOCKED, CANCELLED, or ALL.");

        var sortBy = (query.SortBy ?? "id").Trim().ToLowerInvariant();
        var sortDir = (query.SortDir ?? "desc").Trim().ToLowerInvariant();
        if (sortDir is not ("asc" or "desc"))
            throw new ArgumentException("sortDir must be asc or desc.");

        IQueryable<StoreOrder> q = _db.StoreOrders.AsNoTracking()
            .Where(x => x.FranchiseId == franchiseId);

        if (status != "ALL")
            q = q.Where(x => x.Status == status);

        if (query.FromDate.HasValue)
            q = q.Where(x => x.OrderDate >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            q = q.Where(x => x.OrderDate <= query.ToDate.Value);

        var total = await q.CountAsync(ct);

        q = (sortBy, sortDir) switch
        {
            ("date", "asc") => q.OrderBy(x => x.OrderDate),
            ("date", "desc") => q.OrderByDescending(x => x.OrderDate),
            ("createdat", "asc") => q.OrderBy(x => x.CreatedAt),
            ("createdat", "desc") => q.OrderByDescending(x => x.CreatedAt),
            ("id", "asc") => q.OrderBy(x => x.StoreOrderId),
            _ => q.OrderByDescending(x => x.StoreOrderId)
        };

        var ids = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => x.StoreOrderId)
            .ToListAsync(ct);

        // load details for those ids
        var orders = await _db.StoreOrders.AsNoTracking()
            .Where(x => x.FranchiseId == franchiseId && ids.Contains(x.StoreOrderId))
            .Include(x => x.Items)
            .ThenInclude(i => i.Product)
            .ToListAsync(ct);

        // keep same ordering as ids
        var map = orders.ToDictionary(x => x.StoreOrderId);
        var result = ids.Where(map.ContainsKey).Select(id => ToDto(map[id])).ToList();

        return PagedResult<StoreOrderResponse>.Create(result, page, pageSize, total);
    }

    public async Task<StoreOrderResponse> GetByIdAsync(int franchiseId, int orderId, CancellationToken ct = default)
    {
        RequireRoles(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff);
        await _access.EnsureCanAccessAsync(franchiseId, ct);

        return await GetByIdInternalAsync(franchiseId, orderId, ct);
    }

    public async Task<StoreOrderResponse> LockAsync(int franchiseId, int orderId, CancellationToken ct = default)
    {
        RequireRoles(RoleNames.Admin, RoleNames.Manager, RoleNames.KitchenStaff);

        var order = await _db.StoreOrders
            .Include(x => x.Franchise)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.StoreOrderId == orderId && x.FranchiseId == franchiseId, ct);

        if (order is null)
            throw new KeyNotFoundException($"StoreOrder {orderId} not found.");

        await EnsureCanLockOrderAsync(order, ct);

        if (order.Status == StoreOrderStatus.Cancelled)
            throw new InvalidOperationException("Cannot lock a CANCELLED order.");

        if (order.Status == StoreOrderStatus.Locked)
            throw new InvalidOperationException("Order is already LOCKED.");

        if (order.Status != StoreOrderStatus.Submitted)
            throw new InvalidOperationException("Only SUBMITTED orders can be locked.");

        var now = DateTime.UtcNow;
        var old = new { order.Status, order.LockedAt, order.UpdatedAt };

        order.Status = StoreOrderStatus.Locked;
        order.LockedAt = now;
        order.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "STORE_ORDER_LOCK",
            franchiseId: franchiseId,
            entityName: "StoreOrder",
            entityId: order.StoreOrderId,
            oldObj: old,
            newObj: new { order.Status, order.LockedAt, order.UpdatedAt },
            reason: null,
            ct: ct);

        return await GetByIdInternalAsync(franchiseId, orderId, ct);
    }

    public async Task<PagedResult<IncomingOrderResponse>> SearchIncomingAsync(
    int centralKitchenId,
    StoreOrderListQuery query,
    CancellationToken ct = default)
    {
        RequireRoles(RoleNames.Admin, RoleNames.Manager, RoleNames.KitchenStaff);
        await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

        query ??= new StoreOrderListQuery();

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        if (pageSize > 200) pageSize = 200;

        var status = (query.Status ?? "ALL").Trim().ToUpperInvariant();
        if (status is not ("ALL" or StoreOrderStatus.Draft or StoreOrderStatus.Submitted or StoreOrderStatus.Locked or StoreOrderStatus.Cancelled))
            throw new ArgumentException("status must be DRAFT, SUBMITTED, LOCKED, CANCELLED, or ALL.");

        var sortBy = (query.SortBy ?? "id").Trim().ToLowerInvariant();
        var sortDir = (query.SortDir ?? "desc").Trim().ToLowerInvariant();
        if (sortDir is not ("asc" or "desc"))
            throw new ArgumentException("sortDir must be asc or desc.");

        IQueryable<StoreOrder> q = _db.StoreOrders
            .AsNoTracking()
            .Where(x => x.Franchise.CentralKitchenId == centralKitchenId);

        if (status != "ALL")
            q = q.Where(x => x.Status == status);

        if (query.FromDate.HasValue)
            q = q.Where(x => x.OrderDate >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            q = q.Where(x => x.OrderDate <= query.ToDate.Value);

        var total = await q.CountAsync(ct);

        q = (sortBy, sortDir) switch
        {
            ("date", "asc") => q.OrderBy(x => x.OrderDate),
            ("date", "desc") => q.OrderByDescending(x => x.OrderDate),
            ("createdat", "asc") => q.OrderBy(x => x.CreatedAt),
            ("createdat", "desc") => q.OrderByDescending(x => x.CreatedAt),
            ("id", "asc") => q.OrderBy(x => x.StoreOrderId),
            _ => q.OrderByDescending(x => x.StoreOrderId)
        };

        var ids = await q.Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.StoreOrderId)
            .ToListAsync(ct);

        var orders = await _db.StoreOrders
            .AsNoTracking()
            .Where(x => ids.Contains(x.StoreOrderId))
            .Include(x => x.Franchise)
            .Include(x => x.Items)
            .ThenInclude(i => i.Product)
            .ToListAsync(ct);

        var map = orders.ToDictionary(x => x.StoreOrderId);
        var result = ids.Where(map.ContainsKey)
            .Select(id => ToIncomingDto(map[id]))
            .ToList();

        return PagedResult<IncomingOrderResponse>.Create(result, page, pageSize, total);
    }

    public async Task<IncomingOrderResponse> GetIncomingByIdAsync(
        int centralKitchenId,
        int orderId,
        CancellationToken ct = default)
    {
        RequireRoles(RoleNames.Admin, RoleNames.Manager, RoleNames.KitchenStaff);
        await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

        var order = await _db.StoreOrders
            .AsNoTracking()
            .Where(x => x.StoreOrderId == orderId && x.Franchise.CentralKitchenId == centralKitchenId)
            .Include(x => x.Franchise)
            .Include(x => x.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(ct);

        if (order is null)
            throw new KeyNotFoundException($"StoreOrder {orderId} not found.");

        return ToIncomingDto(order);
    }
    // ----------------- helpers -----------------

    private void RequireRoles(params string[] allowedRoles)
    {
        foreach (var role in allowedRoles)
        {
            if (_current.IsInRole(role))
                return;
        }

        throw new ForbiddenAccessException("You do not have permission to perform this action.");
    }

    private async Task EnsureCanEditAsync(StoreOrder order, CancellationToken ct)
    {
        if (order.Status == StoreOrderStatus.Cancelled)
            throw new InvalidOperationException("Cannot edit a CANCELLED order.");

        if (order.LockedAt.HasValue && DateTime.UtcNow >= order.LockedAt.Value)
        {
            // once locked, normalize status for tracking
            if (order.Status == StoreOrderStatus.Submitted)
            {
                order.Status = StoreOrderStatus.Locked;
                order.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            throw new InvalidOperationException("Order is locked. Edit is not allowed (FR-039).");
        }
    }

    private async Task EnsureCanLockOrderAsync(StoreOrder order, CancellationToken ct)
    {
        if (_current.IsInRole(RoleNames.Admin) || _current.IsInRole(RoleNames.Manager))
            return;

        if (_current.IsInRole(RoleNames.KitchenStaff))
        {
            if (order.Franchise is null)
                throw new InvalidOperationException("Store order franchise context is missing.");

            await _access.EnsureCanAccessCentralKitchenAsync(order.Franchise.CentralKitchenId, ct);
            return;
        }

        throw new ForbiddenAccessException("You do not have permission to lock this store order.");
    }

    private async Task EnforceFutureOrderLimitAsync(DateOnly orderDate, CancellationToken ct)
    {
        var limitDays = await GetIntSettingAsync(SettingKeys.FutureOrderLimitDays, fallback: 7, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (orderDate < today)
            throw new ArgumentException("OrderDate cannot be in the past.");

        if (orderDate > today.AddDays(limitDays))
            throw new ArgumentException($"OrderDate exceeds future order limit ({limitDays} days).");
    }

    private async Task<int> GetIntSettingAsync(string key, int fallback, CancellationToken ct)
    {
        var raw = await _db.SystemSettings.AsNoTracking()
            .Where(x => x.Key == key)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);

        return int.TryParse(raw, out var v) && v > 0 ? v : fallback;
    }

    private async Task<StoreOrderResponse> GetByIdInternalAsync(int franchiseId, int orderId, CancellationToken ct)
    {
        var order = await _db.StoreOrders
            .AsNoTracking()
            .Where(x => x.StoreOrderId == orderId && x.FranchiseId == franchiseId)
            .Include(x => x.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(ct);

        if (order is null)
            throw new KeyNotFoundException($"StoreOrder {orderId} not found.");

        return ToDto(order);
    }

    private static StoreOrderResponse ToDto(StoreOrder order)
        => new StoreOrderResponse
        {
            StoreOrderId = order.StoreOrderId,
            FranchiseId = order.FranchiseId,
            Status = order.Status,
            OrderDate = order.OrderDate,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            SubmittedAt = order.SubmittedAt,
            LockedAt = order.LockedAt,
            CancelledAt = order.CancelledAt,
            CancelReason = order.CancelReason,
            Items = order.Items
                .Select(i => new StoreOrderItemResponse
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? "(unknown)",
                    Unit = i.Product?.Unit ?? "",
                    Quantity = i.Quantity
                })
                .ToList()
        };

    private static IncomingOrderResponse ToIncomingDto(StoreOrder order)
    => new IncomingOrderResponse
    {
        StoreOrderId = order.StoreOrderId,
        FranchiseId = order.FranchiseId,
        FranchiseName = order.Franchise?.Name ?? "(unknown)",
        Status = order.Status,
        OrderDate = order.OrderDate,
        CreatedAt = order.CreatedAt,
        UpdatedAt = order.UpdatedAt,
        SubmittedAt = order.SubmittedAt,
        LockedAt = order.LockedAt,
        CancelledAt = order.CancelledAt,
        CancelReason = order.CancelReason,
        Items = order.Items
            .Select(i => new IncomingOrderItemResponse
            {
                ProductId = i.ProductId,
                ProductName = i.Product?.Name ?? "(unknown)",
                Unit = i.Product?.Unit ?? "",
                Quantity = i.Quantity
            })
            .ToList()
    };

    private async Task AddAuditAsync(string action, int franchiseId, string entityName, int entityId, object? oldObj, object? newObj, string? reason, CancellationToken ct)
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

}