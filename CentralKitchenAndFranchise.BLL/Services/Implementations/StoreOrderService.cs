using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Extensions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.StoreOrders;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.StoreOrders;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

        var hasProductItems = request.Items is not null && request.Items.Count > 0;
        var hasIngredientItems = request.IngredientItems is not null && request.IngredientItems.Count > 0;

        if (!hasProductItems && !hasIngredientItems)
            throw new ArgumentException("At least one product item or ingredient item is required.");

        await EnforceFutureOrderLimitAsync(request.OrderDate, ct);

        var productMap = (request.Items ?? new List<CreateStoreOrderItemRequest>())
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var ingredientMap = (request.IngredientItems ?? new List<CreateStoreOrderIngredientItemRequest>())
            .GroupBy(x => x.IngredientId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        foreach (var (productId, qty) in productMap)
        {
            if (productId <= 0) throw new ArgumentException("ProductId must be positive.");
            if (qty <= 0) throw new ArgumentException("Product quantity must be > 0.");
        }

        foreach (var (ingredientId, qty) in ingredientMap)
        {
            if (ingredientId <= 0) throw new ArgumentException("IngredientId must be positive.");
            if (qty <= 0) throw new ArgumentException("Ingredient quantity must be > 0.");
        }

        if (productMap.Count > 0)
        {
            var allowedProductIds = await _db.StoreCatalogs
                .AsNoTracking()
                .Where(x => x.FranchiseId == franchiseId && x.Status == "ACTIVE")
                .Select(x => x.ProductId)
                .ToListAsync(ct);

            var invalidProducts = productMap.Keys.Where(pid => !allowedProductIds.Contains(pid)).ToList();
            if (invalidProducts.Count > 0)
                throw new ArgumentException("Order contains products not assigned to store catalog.");

            var activeProducts = await _db.Products
                .AsNoTracking()
                .Where(x => productMap.Keys.Contains(x.ProductId) && x.Status == "ACTIVE")
                .Select(x => x.ProductId)
                .ToListAsync(ct);

            var missingProducts = productMap.Keys.Where(pid => !activeProducts.Contains(pid)).ToList();
            if (missingProducts.Count > 0)
                throw new ArgumentException("Some products are not ACTIVE or not found.");
        }

        if (ingredientMap.Count > 0)
        {
            var activeIngredients = await _db.Ingredients
                .AsNoTracking()
                .Where(x => ingredientMap.Keys.Contains(x.IngredientId) && x.Status == "ACTIVE")
                .Select(x => x.IngredientId)
                .ToListAsync(ct);

            var missingIngredients = ingredientMap.Keys.Where(id => !activeIngredients.Contains(id)).ToList();
            if (missingIngredients.Count > 0)
                throw new ArgumentException("Some ingredients are not ACTIVE or not found.");
        }

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

        foreach (var (productId, qty) in productMap)
        {
            _db.StoreOrderItems.Add(new StoreOrderItem
            {
                StoreOrderId = order.StoreOrderId,
                ProductId = productId,
                Quantity = qty
            });
        }

        foreach (var (ingredientId, qty) in ingredientMap)
        {
            _db.StoreOrderIngredientItems.Add(new StoreOrderIngredientItem
            {
                StoreOrderId = order.StoreOrderId,
                IngredientId = ingredientId,
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
            newObj: new
            {
                order.StoreOrderId,
                order.Status,
                order.OrderDate,
                Items = productMap,
                IngredientItems = ingredientMap
            },
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
            .Include(x => x.IngredientItems)
            .FirstOrDefaultAsync(x => x.StoreOrderId == orderId && x.FranchiseId == franchiseId, ct);

        if (order is null) throw new KeyNotFoundException($"StoreOrder {orderId} not found.");

        await EnsureCanEditAsync(order, ct);
        await EnforceFutureOrderLimitAsync(request.OrderDate, ct);

        var hasProductItems = request.Items is not null && request.Items.Count > 0;
        var hasIngredientItems = request.IngredientItems is not null && request.IngredientItems.Count > 0;

        if (!hasProductItems && !hasIngredientItems)
            throw new ArgumentException("At least one product item or ingredient item is required.");

        var productMap = (request.Items ?? new List<UpdateStoreOrderItemRequest>())
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var ingredientMap = (request.IngredientItems ?? new List<UpdateStoreOrderIngredientItemRequest>())
            .GroupBy(x => x.IngredientId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        foreach (var (productId, qty) in productMap)
        {
            if (productId <= 0) throw new ArgumentException("ProductId must be positive.");
            if (qty <= 0) throw new ArgumentException("Product quantity must be > 0.");
        }

        foreach (var (ingredientId, qty) in ingredientMap)
        {
            if (ingredientId <= 0) throw new ArgumentException("IngredientId must be positive.");
            if (qty <= 0) throw new ArgumentException("Ingredient quantity must be > 0.");
        }

        if (productMap.Count > 0)
        {
            var allowedProductIds = await _db.StoreCatalogs
                .AsNoTracking()
                .Where(x => x.FranchiseId == franchiseId && x.Status == "ACTIVE")
                .Select(x => x.ProductId)
                .ToListAsync(ct);

            var invalidProducts = productMap.Keys.Where(pid => !allowedProductIds.Contains(pid)).ToList();
            if (invalidProducts.Count > 0)
                throw new ArgumentException("Order contains products not assigned to store catalog.");

            var activeProducts = await _db.Products
                .AsNoTracking()
                .Where(x => productMap.Keys.Contains(x.ProductId) && x.Status == "ACTIVE")
                .Select(x => x.ProductId)
                .ToListAsync(ct);

            var missingProducts = productMap.Keys.Where(pid => !activeProducts.Contains(pid)).ToList();
            if (missingProducts.Count > 0)
                throw new ArgumentException("Some products are not ACTIVE or not found.");
        }

        if (ingredientMap.Count > 0)
        {
            var activeIngredients = await _db.Ingredients
                .AsNoTracking()
                .Where(x => ingredientMap.Keys.Contains(x.IngredientId) && x.Status == "ACTIVE")
                .Select(x => x.IngredientId)
                .ToListAsync(ct);

            var missingIngredients = ingredientMap.Keys.Where(id => !activeIngredients.Contains(id)).ToList();
            if (missingIngredients.Count > 0)
                throw new ArgumentException("Some ingredients are not ACTIVE or not found.");
        }

        var old = new
        {
            order.Status,
            order.OrderDate,
            Items = order.Items.Select(i => new { i.ProductId, i.Quantity }).ToList(),
            IngredientItems = order.IngredientItems.Select(i => new { i.IngredientId, i.Quantity }).ToList()
        };

        order.OrderDate = request.OrderDate;
        order.UpdatedAt = DateTime.UtcNow;

        _db.StoreOrderItems.RemoveRange(order.Items);
        _db.StoreOrderIngredientItems.RemoveRange(order.IngredientItems);
        await _db.SaveChangesAsync(ct);

        foreach (var (productId, qty) in productMap)
        {
            _db.StoreOrderItems.Add(new StoreOrderItem
            {
                StoreOrderId = order.StoreOrderId,
                ProductId = productId,
                Quantity = qty
            });
        }

        foreach (var (ingredientId, qty) in ingredientMap)
        {
            _db.StoreOrderIngredientItems.Add(new StoreOrderIngredientItem
            {
                StoreOrderId = order.StoreOrderId,
                IngredientId = ingredientId,
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
            newObj: new
            {
                order.Status,
                order.OrderDate,
                Items = productMap,
                IngredientItems = ingredientMap
            },
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
            .Include(x => x.IngredientItems)
            .FirstOrDefaultAsync(x => x.StoreOrderId == orderId && x.FranchiseId == franchiseId, ct);

        if (order is null) throw new KeyNotFoundException($"StoreOrder {orderId} not found.");

        if (order.Status != StoreOrderStatus.Draft)
            throw new InvalidOperationException("Only DRAFT orders can be submitted.");

        if (order.Items.Count == 0 && order.IngredientItems.Count == 0)
            throw new ArgumentException("Cannot submit an order with no items.");

        var now = DateTime.UtcNow;

        order.Status = StoreOrderStatus.Submitted;
        order.SubmittedAt = now;
        order.LockedAt = null;
        order.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "STORE_ORDER_SUBMIT",
            franchiseId: franchiseId,
            entityName: "StoreOrder",
            entityId: order.StoreOrderId,
            oldObj: new { Status = StoreOrderStatus.Draft },
            newObj: new { order.Status, order.SubmittedAt },
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

        if (order.Status == StoreOrderStatus.Locked)
            throw new InvalidOperationException("Order is locked. Cancel is not allowed.");

        var hasCommittedDelivery = await _db.DeliveryPlans
            .Where(x => x.StoreOrderId == order.StoreOrderId)
            .Join(
                _db.Deliveries,
                plan => plan.DeliveryPlanId,
                delivery => delivery.DeliveryPlanId,
                (_, delivery) => delivery)
            .AnyAsync(x => x.IsStockCommitted, ct);

        if (hasCommittedDelivery)
            throw new InvalidOperationException("Order cannot be cancelled after delivery stock has been committed.");

        if (await IsSubmittedEditWindowExpiredAsync(order, ct))
            throw new InvalidOperationException("Order edit window has expired. Cancel is not allowed.");

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
        if (!string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase)
            && !StoreOrderStatus.IsValid(status))
        {
            throw new ArgumentException("Invalid status.");
        }

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
            .Include(x => x.IngredientItems)
                .ThenInclude(i => i.Ingredient)
            .ToListAsync(ct);

        var productSnapshotMap = await LoadProductForwardSnapshotMapAsync(ids, ct);
        var ingredientSnapshotMap = await LoadIngredientForwardSnapshotMapAsync(ids, ct);

        // keep same ordering as ids
        var map = orders.ToDictionary(x => x.StoreOrderId);
        var result = ids
            .Where(map.ContainsKey)
            .Select(id =>
            {
                productSnapshotMap.TryGetValue(id, out var orderProductSnapshot);
                ingredientSnapshotMap.TryGetValue(id, out var orderIngredientSnapshot);

                var resolvedProductSnapshotMap = ResolveForwardSnapshotByProduct(map[id], orderProductSnapshot);
                var resolvedIngredientSnapshotMap = ResolveForwardSnapshotByIngredient(map[id], orderIngredientSnapshot);
                return ToDto(map[id], resolvedProductSnapshotMap, resolvedIngredientSnapshotMap);
            })
            .ToList();

        return PagedResult<StoreOrderResponse>.Create(result, page, pageSize, total);
    }

    public async Task<StoreOrderResponse> GetByIdAsync(int franchiseId, int orderId, CancellationToken ct = default)
    {
        RequireRoles(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff,RoleNames.SupplyCoordinator);
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
            .Include(x => x.IngredientItems)
                .ThenInclude(i => i.Ingredient)
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
            .Include(x => x.IngredientItems)
            .ThenInclude(i => i.Ingredient)
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

        if (order.Status == StoreOrderStatus.Locked)
            throw new InvalidOperationException("Order is locked. Edit is not allowed (FR-039).");

        if (await IsSubmittedEditWindowExpiredAsync(order, ct))
            throw new InvalidOperationException("Order edit window has expired. Edit is not allowed (FR-039).");
    }

    private async Task<bool> IsSubmittedEditWindowExpiredAsync(StoreOrder order, CancellationToken ct)
    {
        if (order.Status != StoreOrderStatus.Submitted || !order.SubmittedAt.HasValue)
            return false;

        var minutes = await GetIntSettingAsync(SettingKeys.OrderEditWindowMinutes, fallback: 30, ct);
        var editWindowEndsAt = order.SubmittedAt.Value.AddMinutes(minutes);
        return DateTime.UtcNow >= editWindowEndsAt;
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
            .Include(x => x.IngredientItems)
                .ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(ct);

        if (order is null)
            throw new KeyNotFoundException($"StoreOrder {orderId} not found.");

        var productSnapshotMap = await LoadProductForwardSnapshotMapAsync(new List<int> { order.StoreOrderId }, ct);
        productSnapshotMap.TryGetValue(order.StoreOrderId, out var orderProductSnapshot);

        var ingredientSnapshotMap = await LoadIngredientForwardSnapshotMapAsync(new List<int> { order.StoreOrderId }, ct);
        ingredientSnapshotMap.TryGetValue(order.StoreOrderId, out var orderIngredientSnapshot);

        var resolvedProductSnapshotMap = ResolveForwardSnapshotByProduct(order, orderProductSnapshot);
        var resolvedIngredientSnapshotMap = ResolveForwardSnapshotByIngredient(order, orderIngredientSnapshot);

        return ToDto(order, resolvedProductSnapshotMap, resolvedIngredientSnapshotMap);
    }

    private static StoreOrderResponse ToDto(
        StoreOrder order,
        Dictionary<int, ResolvedForwardSnapshotLine>? resolvedProductSnapshotMap = null,
        Dictionary<int, ResolvedForwardSnapshotLine>? resolvedIngredientSnapshotMap = null)
    {
        resolvedProductSnapshotMap ??= new Dictionary<int, ResolvedForwardSnapshotLine>();
        resolvedIngredientSnapshotMap ??= new Dictionary<int, ResolvedForwardSnapshotLine>();

        return new StoreOrderResponse
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
                .Select(i =>
                {
                    resolvedProductSnapshotMap.TryGetValue(i.ProductId, out var resolved);

                    return new StoreOrderItemResponse
                    {
                        ProductId = i.ProductId,
                        ProductName = i.Product?.Name ?? "(unknown)",
                        Unit = i.Product?.Unit ?? "",
                        Quantity = i.Quantity,
                        ForwardedQuantity = resolved?.ForwardedQuantity ?? 0m,
                        DroppedQuantity = resolved?.DroppedQuantity ?? 0m,
                        IsDroppedFromForward = resolved?.IsDropped ?? false,
                        DropReason = resolved?.DropReason
                    };
                })
                .ToList(),

            IngredientItems = order.IngredientItems
                .Select(i =>
                {
                    resolvedIngredientSnapshotMap.TryGetValue(i.IngredientId, out var resolved);

                    return new StoreOrderIngredientItemResponse
                    {
                        IngredientId = i.IngredientId,
                        IngredientName = i.Ingredient?.Name ?? "(unknown)",
                        Unit = i.Ingredient?.Unit ?? "",
                        Quantity = i.Quantity,
                        ForwardedQuantity = resolved?.ForwardedQuantity ?? 0m,
                        DroppedQuantity = resolved?.DroppedQuantity ?? 0m,
                        IsDroppedFromForward = resolved?.IsDropped ?? false,
                        DropReason = resolved?.DropReason
                    };
                })
                .ToList()
        };
    }

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
                .ToList(),

            IngredientItems = order.IngredientItems
                .Select(i => new IncomingOrderIngredientItemResponse
                {
                    IngredientId = i.IngredientId,
                    IngredientName = i.Ingredient?.Name ?? "(unknown)",
                    Unit = i.Ingredient?.Unit ?? "",
                    Quantity = i.Quantity
                })
                .ToList()
        };

    private sealed class ForwardSnapshotLine
    {
        public int ItemId { get; set; }
        public decimal RequestedQuantity { get; set; }
        public decimal ForwardedQuantity { get; set; }
        public decimal DroppedQuantity { get; set; }
        public bool IsDropped { get; set; }
        public string? DropReason { get; set; }
    }

    private Dictionary<int, ResolvedForwardSnapshotLine> ResolveForwardSnapshotByProduct(
        StoreOrder order,
        Dictionary<int, ForwardSnapshotLine>? orderSnapshot)
    {
        orderSnapshot ??= new Dictionary<int, ForwardSnapshotLine>();

        return order.Items
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    orderSnapshot.TryGetValue(g.Key, out var snapshot);

                    return StoreOrderForwardSnapshotHelper.Resolve(
                        order.Status,
                        "ProductId",
                        g.Key,
                        g.Sum(x => x.Quantity),
                        snapshot is not null,
                        snapshot?.RequestedQuantity ?? 0m,
                        snapshot?.ForwardedQuantity ?? 0m,
                        snapshot?.IsDropped ?? false,
                        snapshot?.DropReason);
                });
    }

    private Dictionary<int, ResolvedForwardSnapshotLine> ResolveForwardSnapshotByIngredient(
        StoreOrder order,
        Dictionary<int, ForwardSnapshotLine>? orderSnapshot)
    {
        orderSnapshot ??= new Dictionary<int, ForwardSnapshotLine>();

        return order.IngredientItems
            .GroupBy(x => x.IngredientId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    orderSnapshot.TryGetValue(g.Key, out var snapshot);

                    return StoreOrderForwardSnapshotHelper.Resolve(
                        order.Status,
                        "IngredientId",
                        g.Key,
                        g.Sum(x => x.Quantity),
                        snapshot is not null,
                        snapshot?.RequestedQuantity ?? 0m,
                        snapshot?.ForwardedQuantity ?? 0m,
                        snapshot?.IsDropped ?? false,
                        snapshot?.DropReason);
                });
    }

    private async Task<Dictionary<int, Dictionary<int, ForwardSnapshotLine>>> LoadProductForwardSnapshotMapAsync(
        List<int> orderIds,
        CancellationToken ct)
    {
        if (orderIds.Count == 0)
            return new();

        var lines = await _db.DeliveryProductItems
            .AsNoTracking()
            .Include(x => x.Delivery)
                .ThenInclude(x => x.DeliveryPlan)
            .Where(x =>
                x.Delivery.DeliveryPlan.StoreOrderId.HasValue &&
                orderIds.Contains(x.Delivery.DeliveryPlan.StoreOrderId.Value))
            .ToListAsync(ct);

        return lines
            .GroupBy(x => x.Delivery.DeliveryPlan.StoreOrderId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g
                    .GroupBy(x => x.ProductId)
                    .Select(x =>
                    {
                        var requested = x.Sum(i => i.RequestedQuantity > 0 ? i.RequestedQuantity : i.Quantity);
                        var forwarded = x.Sum(i => i.Quantity);
                        var reasons = x
                            .Where(i => !string.IsNullOrWhiteSpace(i.DropReason))
                            .Select(i => i.DropReason!.Trim())
                            .Distinct()
                            .ToList();

                        return new ForwardSnapshotLine
                        {
                            ItemId = x.Key,
                            RequestedQuantity = requested,
                            ForwardedQuantity = forwarded,
                            DroppedQuantity = Math.Max(requested - forwarded, 0m),
                            IsDropped = x.Any(i => i.IsDropped),
                            DropReason = reasons.Count == 0 ? null : string.Join(" | ", reasons)
                        };
                    })
                    .ToDictionary(x => x.ItemId, x => x));
    }

    private async Task<Dictionary<int, Dictionary<int, ForwardSnapshotLine>>> LoadIngredientForwardSnapshotMapAsync(
        List<int> orderIds,
        CancellationToken ct)
    {
        if (orderIds.Count == 0)
            return new();

        var lines = await _db.DeliveryIngredientItems
            .AsNoTracking()
            .Include(x => x.Delivery)
                .ThenInclude(x => x.DeliveryPlan)
            .Where(x =>
                x.Delivery.DeliveryPlan.StoreOrderId.HasValue &&
                orderIds.Contains(x.Delivery.DeliveryPlan.StoreOrderId.Value))
            .ToListAsync(ct);

        return lines
            .GroupBy(x => x.Delivery.DeliveryPlan.StoreOrderId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g
                    .GroupBy(x => x.IngredientId)
                    .Select(x =>
                    {
                        var requested = x.Sum(i => i.RequestedQuantity > 0 ? i.RequestedQuantity : i.Quantity);
                        var forwarded = x.Sum(i => i.Quantity);
                        var reasons = x
                            .Where(i => !string.IsNullOrWhiteSpace(i.DropReason))
                            .Select(i => i.DropReason!.Trim())
                            .Distinct()
                            .ToList();

                        return new ForwardSnapshotLine
                        {
                            ItemId = x.Key,
                            RequestedQuantity = requested,
                            ForwardedQuantity = forwarded,
                            DroppedQuantity = Math.Max(requested - forwarded, 0m),
                            IsDropped = x.Any(i => i.IsDropped),
                            DropReason = reasons.Count == 0 ? null : string.Join(" | ", reasons)
                        };
                    })
                    .ToDictionary(x => x.ItemId, x => x));
    }

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