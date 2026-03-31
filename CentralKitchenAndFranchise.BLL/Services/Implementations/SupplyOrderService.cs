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

public class SupplyOrderService : ISupplyOrderService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFranchiseAccessService _access;
    private readonly IInventoryTransferService _transferService;

    public SupplyOrderService(
        AppDbContext db,
        ICurrentUserService current,
        IFranchiseAccessService access,
        IInventoryTransferService transferService)
    {
        _db = db;
        _current = current;
        _access = access;
        _transferService = transferService;
    }

    public async Task<List<SupplyOrderQueueItemResponse>> GetQueueAsync(CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.SupplyCoordinator);

        int? scopedCentralKitchenId = null;
        if (_current.IsInRole(RoleNames.SupplyCoordinator))
        {
            scopedCentralKitchenId = await _access.GetCurrentAssignedCentralKitchenIdAsync(ct);
        }

        var statuses = new[]
        {
        StoreOrderStatus.ForwardedToSupply,
        StoreOrderStatus.Preparing,
        StoreOrderStatus.ReadyToDeliver,
        StoreOrderStatus.InTransit
    };

        var query = _db.StoreOrders
            .AsNoTracking()
            .Include(x => x.Franchise)
            .Include(x => x.Items)
                .ThenInclude(i => i.Product)
            .Include(x => x.IngredientItems)
                .ThenInclude(i => i.Ingredient)
            .Where(x => statuses.Contains(x.Status));

        if (scopedCentralKitchenId.HasValue)
        {
            query = query.Where(x => x.Franchise.CentralKitchenId == scopedCentralKitchenId.Value);
        }

        var orders = await query
            .OrderByDescending(x => x.ForwardedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.StoreOrderId)
            .ToListAsync(ct);

        var orderIds = orders.Select(x => x.StoreOrderId).ToList();

        var productSnapshotMap = await LoadProductForwardSnapshotMapAsync(orderIds, ct);
        var ingredientSnapshotMap = await LoadIngredientForwardSnapshotMapAsync(orderIds, ct);

        var userIds = orders
            .Where(x => x.ForwardedByUserId.HasValue)
            .Select(x => x.ForwardedByUserId!.Value)
            .Distinct()
            .ToList();

        var userMap = userIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Users
                .AsNoTracking()
                .Where(x => userIds.Contains(x.UserId))
                .ToDictionaryAsync(x => x.UserId, x => x.Username, ct);

        return orders.Select(x =>
        {
            productSnapshotMap.TryGetValue(x.StoreOrderId, out var orderProductSnapshot);
            ingredientSnapshotMap.TryGetValue(x.StoreOrderId, out var orderIngredientSnapshot);

            var resolvedProductSnapshotMap = ResolveForwardSnapshotByProduct(x, orderProductSnapshot);
            var resolvedIngredientSnapshotMap = ResolveForwardSnapshotByIngredient(x, orderIngredientSnapshot);

            var items = x.Items
                .OrderBy(i => i.ProductId)
                .Select(i =>
                {
                    var resolved = resolvedProductSnapshotMap[i.ProductId];

                    return new SupplyOrderQueueItemLineResponse
                    {
                        ProductId = i.ProductId,
                        ProductName = i.Product?.Name ?? "(unknown)",
                        Sku = i.Product?.Sku,
                        Unit = i.Product?.Unit ?? "",
                        Quantity = i.Quantity,

                        ForwardedQuantity = resolved.ForwardedQuantity,
                        DroppedQuantity = resolved.DroppedQuantity,
                        IsDroppedFromForward = resolved.IsDropped,
                        DropReason = resolved.DropReason,

                        HasForwardSnapshot = resolved.HasSnapshot,
                        IsForwardSnapshotConsistent = resolved.IsConsistent,
                        ForwardSnapshotWarning = resolved.Warning,
                        RawForwardSnapshotRequestedQuantity = resolved.RawRequestedQuantity,
                        RawForwardSnapshotForwardedQuantity = resolved.RawForwardedQuantity,
                        RawForwardSnapshotDroppedQuantity = resolved.RawDroppedQuantity
                    };
                })
                .ToList();

            var ingredientItems = x.IngredientItems
                .OrderBy(i => i.IngredientId)
                .Select(i =>
                {
                    var resolved = resolvedIngredientSnapshotMap[i.IngredientId];

                    return new SupplyOrderQueueIngredientLineResponse
                    {
                        IngredientId = i.IngredientId,
                        IngredientName = i.Ingredient?.Name ?? "(unknown)",
                        Unit = i.Ingredient?.Unit ?? "",
                        Quantity = i.Quantity,

                        ForwardedQuantity = resolved.ForwardedQuantity,
                        DroppedQuantity = resolved.DroppedQuantity,
                        IsDroppedFromForward = resolved.IsDropped,
                        DropReason = resolved.DropReason,

                        HasForwardSnapshot = resolved.HasSnapshot,
                        IsForwardSnapshotConsistent = resolved.IsConsistent,
                        ForwardSnapshotWarning = resolved.Warning,
                        RawForwardSnapshotRequestedQuantity = resolved.RawRequestedQuantity,
                        RawForwardSnapshotForwardedQuantity = resolved.RawForwardedQuantity,
                        RawForwardSnapshotDroppedQuantity = resolved.RawDroppedQuantity
                    };
                })
                .ToList();

            return new SupplyOrderQueueItemResponse
            {
                StoreOrderId = x.StoreOrderId,
                OrderCode = BuildOrderCode(x.StoreOrderId),
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                RequestedDeliveryDate = x.OrderDate,
                StoreId = x.FranchiseId,
                StoreName = x.Franchise.Name,

                TotalItems = x.Items.Count + x.IngredientItems.Count,
                TotalQuantity = x.Items.Sum(i => i.Quantity) + x.IngredientItems.Sum(i => i.Quantity),

                ForwardedTotalItems = items.Count(i => i.ForwardedQuantity > 0) + ingredientItems.Count(i => i.ForwardedQuantity > 0),
                ForwardedTotalQuantity = items.Sum(i => i.ForwardedQuantity) + ingredientItems.Sum(i => i.ForwardedQuantity),
                DroppedTotalItems = items.Count(i => i.IsDroppedFromForward) + ingredientItems.Count(i => i.IsDroppedFromForward),
                DroppedTotalQuantity = items.Sum(i => i.DroppedQuantity) + ingredientItems.Sum(i => i.DroppedQuantity),

                ForwardedAt = x.ForwardedAt,
                ForwardedBy = x.ForwardedByUserId.HasValue && userMap.TryGetValue(x.ForwardedByUserId.Value, out var username)
                    ? username
                    : null,
                ProcessingNote = x.ProcessingNote,
                ForwardNote = x.ForwardNote,

                Items = items,
                IngredientItems = ingredientItems
            };
        }).ToList();
    }

    // Return final-state supply orders that have left the active supply queue.
    public async Task<PagedResult<SupplyProcessedOrderResponse>> GetHistoryAsync(
        SupplyOrderListQuery query,
        CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.SupplyCoordinator);

        query ??= new SupplyOrderListQuery();

        if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate.Value > query.ToDate.Value)
            throw new ArgumentException("FromDate cannot be greater than ToDate.");

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        if (pageSize > 200)
            pageSize = 200;

        var normalizedStatus = NormalizeProcessedHistoryStatus(query.Status);

        int? scopedCentralKitchenId = null;
        if (_current.IsInRole(RoleNames.SupplyCoordinator))
        {
            scopedCentralKitchenId = await _access.GetCurrentAssignedCentralKitchenIdAsync(ct);
        }

        var finalStatuses = new[]
        {
            StoreOrderStatus.Delivered,
            StoreOrderStatus.ReceivedByStore,
            StoreOrderStatus.Cancelled
        };

        var ordersQuery = _db.StoreOrders
            .AsNoTracking()
            .Include(x => x.Franchise)
            .Include(x => x.Items)
            .Include(x => x.IngredientItems)
            .Where(x =>
                x.ForwardedAt.HasValue &&          // only orders that truly entered supply flow
                finalStatuses.Contains(x.Status));

        if (scopedCentralKitchenId.HasValue)
        {
            ordersQuery = ordersQuery.Where(x => x.Franchise.CentralKitchenId == scopedCentralKitchenId.Value);
        }

        if (query.FranchiseId.HasValue)
        {
            ordersQuery = ordersQuery.Where(x => x.FranchiseId == query.FranchiseId.Value);
        }

        if (!string.Equals(normalizedStatus, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            ordersQuery = ordersQuery.Where(x => x.Status == normalizedStatus);
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var orderIdSearch = TryParseOrderCodeSearch(search);
            var pattern = $"%{search}%";

            if (orderIdSearch.HasValue)
            {
                ordersQuery = ordersQuery.Where(x =>
                    EF.Functions.ILike(x.Franchise.Name, pattern) ||
                    x.StoreOrderId == orderIdSearch.Value);
            }
            else
            {
                ordersQuery = ordersQuery.Where(x =>
                    EF.Functions.ILike(x.Franchise.Name, pattern));
            }
        }

        var orders = await ordersQuery.ToListAsync(ct);
        var orderIds = orders.Select(x => x.StoreOrderId).ToList();

        var deliveryMap = await LoadLatestDeliveryMapForOrdersAsync(orderIds, ct);

        var userIds = orders
            .Where(x => x.ForwardedByUserId.HasValue)
            .Select(x => x.ForwardedByUserId!.Value)
            .Concat(orders.Where(x => x.PreparedByUserId.HasValue).Select(x => x.PreparedByUserId!.Value))
            .Concat(orders.Where(x => x.DeliveryStatusUpdatedByUserId.HasValue).Select(x => x.DeliveryStatusUpdatedByUserId!.Value))
            .Concat(deliveryMap.Values
                .Select(ResolveLatestReceivingReport)
                .Where(x => x?.ReceivedByUserId != null)
                .Select(x => x!.ReceivedByUserId!.Value))
            .Distinct()
            .ToList();

        var userMap = await LoadUsernameMapAsync(userIds, ct);

        var items = orders
            .Select(order =>
            {
                deliveryMap.TryGetValue(order.StoreOrderId, out var delivery);

                var endedByUserId = ResolveEndedByUserId(order, delivery);

                return new SupplyProcessedOrderResponse
                {
                    StoreOrderId = order.StoreOrderId,
                    OrderCode = BuildOrderCode(order.StoreOrderId),
                    Status = order.Status,

                    RequestedDeliveryDate = order.OrderDate,
                    CreatedAt = order.CreatedAt,

                    StoreId = order.FranchiseId,
                    StoreName = order.Franchise.Name,

                    TotalItems = order.Items.Count + order.IngredientItems.Count,
                    TotalQuantity = order.Items.Sum(x => x.Quantity) + order.IngredientItems.Sum(x => x.Quantity),

                    ForwardedAt = order.ForwardedAt,
                    ForwardedBy = order.ForwardedByUserId.HasValue && userMap.TryGetValue(order.ForwardedByUserId.Value, out var forwardedBy)
                        ? forwardedBy
                        : null,

                    PreparedAt = order.PreparedAt,
                    PreparedBy = order.PreparedByUserId.HasValue && userMap.TryGetValue(order.PreparedByUserId.Value, out var preparedBy)
                        ? preparedBy
                        : null,

                    EndedAt = ResolveEndedAt(order, delivery),
                    EndedBy = endedByUserId.HasValue && userMap.TryGetValue(endedByUserId.Value, out var endedBy)
                        ? endedBy
                        : null,
                    EndedNote = ResolveEndedNote(order, delivery),

                    ForwardNote = order.ForwardNote,
                    ProcessingNote = order.ProcessingNote,
                    PreparingNote = order.PreparingNote
                };
            })
            .ToList();

        if (query.FromDate.HasValue)
        {
            items = items
                .Where(x => DateOnly.FromDateTime(x.EndedAt) >= query.FromDate.Value)
                .ToList();
        }

        if (query.ToDate.HasValue)
        {
            items = items
                .Where(x => DateOnly.FromDateTime(x.EndedAt) <= query.ToDate.Value)
                .ToList();
        }

        items = ApplyProcessedHistorySort(items, query.SortBy, query.SortDir);

        var totalItems = items.Count;
        var pagedItems = items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return PagedResult<SupplyProcessedOrderResponse>.Create(
            pagedItems,
            page,
            pageSize,
            totalItems);
    }

    public async Task<OrderWorkflowActionResponse> PrepareDeliveryAsync(
        int orderId,
        PrepareDeliveryRequest request,
        CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.SupplyCoordinator);

        var order = await LoadManagedOrderAsync(orderId, ct);

        if (!string.Equals(order.Status, StoreOrderStatus.ForwardedToSupply, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only FORWARDED_TO_SUPPLY orders can be prepared.");

        if (!order.Items.Any() && !order.IngredientItems.Any())
            throw new InvalidOperationException("Cannot prepare delivery for an empty order.");

        await EnsureDeliveryArtifactsAsync(order, ct);
        await AdjustDeliveryLinesByCurrentStockAsync(order, ct);

        var previewDelivery = await LoadDeliveryForOrderAsync(order.StoreOrderId, asTracking: false, ct);
        if (previewDelivery is null)
            throw new InvalidOperationException("Delivery was not found for this store order.");

        var previewTotalQty = previewDelivery.ProductItems.Sum(x => x.Quantity) + previewDelivery.IngredientItems.Sum(x => x.Quantity);
        if (previewTotalQty <= 0m)
        {
            throw new InvalidOperationException(
                "Cannot prepare delivery because there is no usable stock left to ship for this order. All delivery lines have been adjusted to 0.");
        }

        var delivery = await LoadDeliveryForOrderAsync(order.StoreOrderId, asTracking: true, ct);
        if (delivery is null)
            throw new InvalidOperationException("Delivery was not found for this store order.");

        if (delivery.IsStockCommitted)
            throw new InvalidOperationException("Delivery stock has already been committed for this order.");

        var now = DateTime.UtcNow;
        var currentUserId = _current.UserId;
        var oldStatus = order.Status;
        var preparingNote = string.IsNullOrWhiteSpace(request?.PreparingNote)
            ? null
            : request.PreparingNote.Trim();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await _transferService.CommitDeliveryStockAsync(
                delivery,
                order.FranchiseId,
                now,
                ct);

            delivery.IsStockCommitted = true;

            order.Status = StoreOrderStatus.Preparing;
            order.PreparedAt = now;
            order.PreparedByUserId = currentUserId;
            order.PreparingNote = preparingNote;

            _db.Set<StoreOrderHistory>().Add(new StoreOrderHistory
            {
                StoreOrderId = order.StoreOrderId,
                ActionType = StoreOrderHistoryActions.OrderPreparing,
                ActionLabel = "Supply bắt đầu chuẩn bị giao hàng",
                OldStatus = oldStatus,
                NewStatus = order.Status,
                Note = order.PreparingNote,
                PerformedByUserId = currentUserId,
                PerformedAt = now
            });

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                FranchiseId = order.FranchiseId,
                CentralKitchenId = order.Franchise.CentralKitchenId,
                Action = "STORE_ORDER_PREPARED",
                EntityName = "StoreOrder",
                EntityId = order.StoreOrderId,
                OldDataJson = JsonSerializer.Serialize(new
                {
                    Status = oldStatus,
                    DeliveryIsStockCommitted = false
                }),
                NewDataJson = JsonSerializer.Serialize(new
                {
                    order.Status,
                    order.PreparedAt,
                    order.PreparedByUserId,
                    order.PreparingNote,
                    DeliveryIsStockCommitted = delivery.IsStockCommitted
                }),
                Reason = order.PreparingNote,
                CreatedAt = now
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("Concurrent prepare detected. Please retry.");
        }

        return new OrderWorkflowActionResponse
        {
            StoreOrderId = order.StoreOrderId,
            Status = order.Status,
            PreparedAt = order.PreparedAt,
            PreparedBy = await ResolveUsernameAsync(order.PreparedByUserId, ct),
            PreparingNote = order.PreparingNote,
            Message = "Delivery preparation started successfully."
        };
    }

    public async Task<OrderWorkflowActionResponse> UpdateDeliveryStatusAsync(
        int orderId,
        UpdateSupplyDeliveryStatusRequest request,
        CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.SupplyCoordinator);

        var order = await LoadManagedOrderAsync(orderId, ct);

        var nextStatus = NormalizeDeliveryStatus(request.Status);

        if (string.Equals(nextStatus, StoreOrderStatus.Preparing, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use PrepareDeliveryAsync to move an order into PREPARING because that step commits stock atomically.");

        ValidateDeliveryTransition(order.Status, nextStatus);

        var now = DateTime.UtcNow;
        var currentUserId = _current.UserId;
        var oldStatus = order.Status;

        order.Status = nextStatus;
        order.DeliveryStatusUpdatedAt = now;
        order.DeliveryStatusUpdatedByUserId = currentUserId;
        order.DeliveryStatusNote = string.IsNullOrWhiteSpace(request.StatusNote)
            ? null
            : request.StatusNote.Trim();

        var deliveryPlan = await _db.DeliveryPlans
            .FirstOrDefaultAsync(x => x.StoreOrderId == order.StoreOrderId, ct);

        if (deliveryPlan is null)
            throw new InvalidOperationException("DeliveryPlan was not found for this store order.");

        var delivery = await _db.Deliveries
            .FirstOrDefaultAsync(x => x.DeliveryPlanId == deliveryPlan.DeliveryPlanId, ct);

        if (delivery is null)
            throw new InvalidOperationException("Delivery was not found for this store order.");

        // Sync shipment status xuống aggregate delivery để receiving module còn query được.
        delivery.Status = MapOrderStatusToDeliveryStatus(order.Status);

        if (string.Equals(order.Status, StoreOrderStatus.Delivered, StringComparison.OrdinalIgnoreCase))
        {
            delivery.DeliveredAt = now;
        }

        _db.Set<StoreOrderHistory>().Add(new StoreOrderHistory
        {
            StoreOrderId = order.StoreOrderId,
            ActionType = StoreOrderHistoryActions.DeliveryStatusChanged,
            ActionLabel = $"Cập nhật trạng thái giao hàng: {nextStatus}",
            OldStatus = oldStatus,
            NewStatus = order.Status,
            Note = order.DeliveryStatusNote,
            PerformedByUserId = currentUserId,
            PerformedAt = now
        });

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            FranchiseId = order.FranchiseId,
            CentralKitchenId = order.Franchise.CentralKitchenId,
            Action = "STORE_ORDER_DELIVERY_STATUS_UPDATED",
            EntityName = "StoreOrder",
            EntityId = order.StoreOrderId,
            OldDataJson = JsonSerializer.Serialize(new
            {
                Status = oldStatus
            }),
            NewDataJson = JsonSerializer.Serialize(new
            {
                order.Status,
                order.DeliveryStatusUpdatedAt,
                order.DeliveryStatusUpdatedByUserId,
                order.DeliveryStatusNote,
                DeliveryStatus = delivery.Status,
                delivery.DeliveredAt
            }),
            Reason = order.DeliveryStatusNote,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        return new OrderWorkflowActionResponse
        {
            StoreOrderId = order.StoreOrderId,
            Status = order.Status,
            UpdatedAt = order.DeliveryStatusUpdatedAt,
            UpdatedBy = await ResolveUsernameAsync(order.DeliveryStatusUpdatedByUserId, ct),
            StatusNote = order.DeliveryStatusNote,
            Message = "Delivery status updated successfully."
        };
    }

    /// <summary>
    /// Load order + enforce scope:
    /// - Admin/Manager: pass
    /// - SupplyCoordinator: chỉ được thao tác trên order thuộc CK đang assign
    /// </summary>
    /// <summary>
    /// Load order + enforce scope:
    /// - Admin/Manager: pass
    /// - SupplyCoordinator: chỉ được thao tác trên order thuộc CK đang assign
    /// </summary>
    private async Task<StoreOrder> LoadManagedOrderAsync(int orderId, CancellationToken ct)
    {
        var order = await _db.StoreOrders
            .Include(x => x.Franchise)
            .Include(x => x.Items)
                .ThenInclude(i => i.Product)
            .Include(x => x.IngredientItems)
                .ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(x => x.StoreOrderId == orderId, ct);

        if (order is null)
            throw new KeyNotFoundException($"StoreOrder {orderId} not found.");

        await _access.EnsureCanAccessCentralKitchenAsync(order.Franchise.CentralKitchenId, ct);

        return order;
    }

    private sealed class ForwardSnapshotLine
    {
        public int ItemId { get; set; }
        public decimal RequestedQuantity { get; set; }
        public decimal ForwardedQuantity { get; set; }
        public decimal DroppedQuantity { get; set; }
        public bool IsDropped { get; set; }
        public string? DropReason { get; set; }
    }

    /// <summary>
    /// Adjusts delivery lines to match CURRENT central kitchen stock.
    /// Normalizes duplicate lines, then sets Quantity = Min(forwarded, available).
    /// For linked store-order deliveries, resolves RequestedQuantity from original store order.
    /// </summary>

    private async Task<Delivery?> LoadDeliveryForOrderAsync(
    int storeOrderId,
    bool asTracking,
    CancellationToken ct)
    {
        var deliveryPlanId = await _db.DeliveryPlans
            .AsNoTracking()
            .Where(x => x.StoreOrderId == storeOrderId)
            .Select(x => (int?)x.DeliveryPlanId)
            .FirstOrDefaultAsync(ct);

        if (!deliveryPlanId.HasValue)
            return null;

        var query = _db.Deliveries
            .Include(x => x.ProductItems)
            .Include(x => x.IngredientItems)
            .Where(x => x.DeliveryPlanId == deliveryPlanId.Value);

        if (!asTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(ct);
    }

    private async Task AdjustDeliveryLinesByCurrentStockAsync(StoreOrder order, CancellationToken ct)
    {
        var plan = await _db.DeliveryPlans
            .FirstOrDefaultAsync(x => x.StoreOrderId == order.StoreOrderId, ct);

        if (plan is null) return;

        var delivery = await _db.Deliveries
            .Include(x => x.ProductItems)
            .Include(x => x.IngredientItems)
            .FirstOrDefaultAsync(x => x.DeliveryPlanId == plan.DeliveryPlanId, ct);

        if (delivery is null) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ckId = order.Franchise.CentralKitchenId;

        // --- Load store order source-of-truth for RequestedQuantity ---
        var storeOrderProductQtyMap = await _db.StoreOrderItems
            .AsNoTracking()
            .Where(x => x.StoreOrderId == order.StoreOrderId)
            .GroupBy(x => x.ProductId)
            .ToDictionaryAsync(g => g.Key, g => g.Sum(x => x.Quantity), ct);

        var storeOrderIngredientQtyMap = await _db.StoreOrderIngredientItems
            .AsNoTracking()
            .Where(x => x.StoreOrderId == order.StoreOrderId)
            .GroupBy(x => x.IngredientId)
            .ToDictionaryAsync(g => g.Key, g => g.Sum(x => x.Quantity), ct);

        // --- PRODUCT LINES: Normalize duplicates, then adjust ---
        var productGroups = delivery.ProductItems.GroupBy(x => x.ProductId).ToList();
        foreach (var group in productGroups)
        {
            var keep = group.OrderBy(x => x.DeliveryProductItemId).First();
            var extras = group.OrderBy(x => x.DeliveryProductItemId).Skip(1).ToList();

            if (extras.Count > 0)
            {
                // Resolve RequestedQuantity from store order (not sum of duplicates)
                var resolvedRequestedQty = storeOrderProductQtyMap.TryGetValue(group.Key, out var soQty)
                    ? soQty
                    : keep.RequestedQuantity;

                var mergedQty = Math.Min(group.Sum(x => x.Quantity), resolvedRequestedQty);
                keep.Quantity = mergedQty;
                keep.RequestedQuantity = resolvedRequestedQty;
                _db.DeliveryProductItems.RemoveRange(extras);
            }
        }

        await _db.SaveChangesAsync(ct);

        // Re-load after normalization
        delivery = await _db.Deliveries
            .Include(x => x.ProductItems)
            .Include(x => x.IngredientItems)
            .FirstOrDefaultAsync(x => x.DeliveryId == delivery.DeliveryId, ct);

        if (delivery is null) return;

        // Load available product batches
        var productIds = delivery.ProductItems.Select(x => x.ProductId).Distinct().ToList();
        var productBatches = productIds.Count > 0
            ? (await _db.ProductBatches
                .AsNoTracking()
                .Where(x =>
                    x.CentralKitchenId == ckId &&
                    x.FranchiseId == null &&
                    productIds.Contains(x.ProductId) &&
                    x.Quantity > 0)
                .Include(x => x.Product)
                .ToListAsync(ct))
                .Where(x => x.IsUsableNonExpired(today))
                .ToList()
            : new List<ProductBatch>();

        foreach (var line in delivery.ProductItems)
        {
            var availableQty = productBatches
                .Where(x => x.ProductId == line.ProductId)
                .Sum(x => x.Quantity);

            var actualQty = Math.Min(line.Quantity, availableQty);
            actualQty = Math.Min(actualQty, line.RequestedQuantity);

            line.Quantity = actualQty;
            line.IsDropped = actualQty < line.RequestedQuantity;
            line.DropReason = line.IsDropped
                ? (actualQty == 0m
                    ? $"Fully dropped at prepare \u2013 no usable stock. Required={line.RequestedQuantity}."
                    : $"Partial drop at prepare \u2013 stock reduced. Required={line.RequestedQuantity}, Available={availableQty}, Shipped={actualQty}.")
                : null;
        }

        // --- INGREDIENT LINES: Normalize duplicates, then adjust ---
        var ingredientGroups = delivery.IngredientItems.GroupBy(x => x.IngredientId).ToList();
        foreach (var group in ingredientGroups)
        {
            var keep = group.OrderBy(x => x.DeliveryIngredientItemId).First();
            var extras = group.OrderBy(x => x.DeliveryIngredientItemId).Skip(1).ToList();

            if (extras.Count > 0)
            {
                var resolvedRequestedQty = storeOrderIngredientQtyMap.TryGetValue(group.Key, out var soQty)
                    ? soQty
                    : keep.RequestedQuantity;

                var mergedQty = Math.Min(group.Sum(x => x.Quantity), resolvedRequestedQty);
                keep.Quantity = mergedQty;
                keep.RequestedQuantity = resolvedRequestedQty;
                _db.DeliveryIngredientItems.RemoveRange(extras);
            }
        }

        // Load available ingredient batches
        var ingredientIds = delivery.IngredientItems.Select(x => x.IngredientId).Distinct().ToList();
        var ingredientAvailableMap = ingredientIds.Count > 0
            ? (await _db.IngredientBatches
                .AsNoTracking()
                .Where(x =>
                    x.CentralKitchenId == ckId &&
                    x.FranchiseId == null &&
                    ingredientIds.Contains(x.IngredientId) &&
                    x.Quantity > 0)
                .Include(x => x.Ingredient)
                .ToListAsync(ct))
                .Where(x => x.IsUsableNonExpired(today))
                .GroupBy(x => x.IngredientId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity))
            : new Dictionary<int, decimal>();

        foreach (var line in delivery.IngredientItems)
        {
            var availableQty = ingredientAvailableMap.TryGetValue(line.IngredientId, out var qty) ? qty : 0m;

            var actualQty = Math.Min(line.Quantity, availableQty);
            actualQty = Math.Min(actualQty, line.RequestedQuantity);

            line.Quantity = actualQty;
            line.IsDropped = actualQty < line.RequestedQuantity;
            line.DropReason = line.IsDropped
                ? (actualQty == 0m
                    ? $"Fully dropped at prepare \u2013 no usable stock. Required={line.RequestedQuantity}."
                    : $"Partial drop at prepare \u2013 stock reduced. Required={line.RequestedQuantity}, Available={availableQty}, Shipped={actualQty}.")
                : null;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureDeliveryArtifactsAsync(StoreOrder order, CancellationToken ct)
    {
        var existingPlan = await _db.DeliveryPlans
            .FirstOrDefaultAsync(x => x.StoreOrderId == order.StoreOrderId, ct);

        if (existingPlan is null)
        {
            existingPlan = new DeliveryPlan
            {
                CentralKitchenId = order.Franchise.CentralKitchenId,
                FranchiseId = order.FranchiseId,
                PlannedDate = order.OrderDate,
                StoreOrderId = order.StoreOrderId
            };

            _db.DeliveryPlans.Add(existingPlan);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            existingPlan.CentralKitchenId = order.Franchise.CentralKitchenId;
            existingPlan.FranchiseId = order.FranchiseId;
            existingPlan.PlannedDate = order.OrderDate;
        }

        var delivery = await _db.Deliveries
            .Include(x => x.ProductItems)
            .Include(x => x.IngredientItems)
            .FirstOrDefaultAsync(x => x.DeliveryPlanId == existingPlan.DeliveryPlanId, ct);

        if (delivery is null)
        {
            delivery = new Delivery
            {
                DeliveryPlanId = existingPlan.DeliveryPlanId,
                FromCentralKitchenId = order.Franchise.CentralKitchenId,
                Status = DeliveryStatus.Created,
                CreatedAt = DateTime.UtcNow
            };

            _db.Deliveries.Add(delivery);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            delivery.FromCentralKitchenId = order.Franchise.CentralKitchenId;
        }

        var productSnapshotMap = await LoadProductForwardSnapshotMapAsync(new List<int> { order.StoreOrderId }, ct);
        productSnapshotMap.TryGetValue(order.StoreOrderId, out var productOrderSnapshot);
        var resolvedProductSnapshotMap = ResolveForwardSnapshotByProduct(order, productOrderSnapshot);

        var existingProductMap = delivery.ProductItems
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var line in order.Items)
        {
            resolvedProductSnapshotMap.TryGetValue(line.ProductId, out var resolved);

            var forwardedQty = resolved?.ForwardedQuantity ?? 0m;
            var requestedQty = resolved?.RawRequestedQuantity > 0 ? resolved.RawRequestedQuantity : line.Quantity;
            var isDropped = resolved?.IsDropped ?? false;
            var dropReason = resolved?.DropReason;

            if (existingProductMap.TryGetValue(line.ProductId, out var deliveryLine))
            {
                deliveryLine.Quantity = forwardedQty;
                deliveryLine.RequestedQuantity = requestedQty;
                deliveryLine.IsDropped = isDropped;
                deliveryLine.DropReason = dropReason;
            }
            else
            {
                delivery.ProductItems.Add(new DeliveryProductItem
                {
                    ProductId = line.ProductId,
                    Quantity = forwardedQty,
                    RequestedQuantity = requestedQty,
                    IsDropped = isDropped,
                    DropReason = dropReason
                });
            }
        }

        var ingredientSnapshotMap = await LoadIngredientForwardSnapshotMapAsync(new List<int> { order.StoreOrderId }, ct);
        ingredientSnapshotMap.TryGetValue(order.StoreOrderId, out var ingredientOrderSnapshot);
        var resolvedIngredientSnapshotMap = ResolveForwardSnapshotByIngredient(order, ingredientOrderSnapshot);

        var existingIngredientMap = delivery.IngredientItems
            .GroupBy(x => x.IngredientId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var line in order.IngredientItems)
        {
            resolvedIngredientSnapshotMap.TryGetValue(line.IngredientId, out var resolved);

            var forwardedQty = resolved?.ForwardedQuantity ?? 0m;
            var requestedQty = resolved?.RawRequestedQuantity > 0 ? resolved.RawRequestedQuantity : line.Quantity;
            var isDropped = resolved?.IsDropped ?? false;
            var dropReason = resolved?.DropReason;

            if (existingIngredientMap.TryGetValue(line.IngredientId, out var deliveryLine))
            {
                deliveryLine.Quantity = forwardedQty;
                deliveryLine.RequestedQuantity = requestedQty;
                deliveryLine.IsDropped = isDropped;
                deliveryLine.DropReason = dropReason;
            }
            else
            {
                delivery.IngredientItems.Add(new DeliveryIngredientItem
                {
                    IngredientId = line.IngredientId,
                    Quantity = forwardedQty,
                    RequestedQuantity = requestedQty,
                    IsDropped = isDropped,
                    DropReason = dropReason
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private static string NormalizeDeliveryStatus(string rawStatus)
    {
        var value = (rawStatus ?? string.Empty).Trim().ToUpperInvariant();

        return value switch
        {
            var s when s == StoreOrderStatus.Preparing => StoreOrderStatus.Preparing,
            var s when s == StoreOrderStatus.ReadyToDeliver => StoreOrderStatus.ReadyToDeliver,
            var s when s == StoreOrderStatus.InTransit => StoreOrderStatus.InTransit,
            var s when s == StoreOrderStatus.Delivered => StoreOrderStatus.Delivered,
            _ => throw new ArgumentException($"Unsupported delivery status: {rawStatus}")
        };
    }

    private static void ValidateDeliveryTransition(string currentStatus, string nextStatus)
    {
        var valid = (currentStatus, nextStatus) switch
        {
            (var s, var n) when s == StoreOrderStatus.Preparing && n == StoreOrderStatus.ReadyToDeliver => true,
            (var s, var n) when s == StoreOrderStatus.ReadyToDeliver && n == StoreOrderStatus.InTransit => true,
            (var s, var n) when s == StoreOrderStatus.InTransit && n == StoreOrderStatus.Delivered => true,
            _ => false
        };

        if (!valid)
            throw new InvalidOperationException($"Invalid delivery status transition: {currentStatus} -> {nextStatus}");
    }

    private static string MapOrderStatusToDeliveryStatus(string orderStatus)
    {
        return orderStatus switch
        {
            var s when s == StoreOrderStatus.Preparing => DeliveryStatus.Created,
            var s when s == StoreOrderStatus.ReadyToDeliver => DeliveryStatus.Created,
            var s when s == StoreOrderStatus.InTransit => DeliveryStatus.Shipped,
            var s when s == StoreOrderStatus.Delivered => DeliveryStatus.Delivered,
            var s when s == StoreOrderStatus.Cancelled => DeliveryStatus.Cancelled,
            _ => DeliveryStatus.Created
        };
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

    // Load the latest delivery aggregate for each processed store order.
    private async Task<Dictionary<int, Delivery>> LoadLatestDeliveryMapForOrdersAsync(
        List<int> orderIds,
        CancellationToken ct)
    {
        if (orderIds.Count == 0)
            return new Dictionary<int, Delivery>();

        var deliveries = await _db.Deliveries
            .AsNoTracking()
            .Include(x => x.DeliveryPlan)
            .Include(x => x.ReceivingReports)
            .Where(x =>
                x.DeliveryPlan.StoreOrderId.HasValue &&
                orderIds.Contains(x.DeliveryPlan.StoreOrderId.Value))
            .ToListAsync(ct);

        return deliveries
            .GroupBy(x => x.DeliveryPlan.StoreOrderId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(x => x.ConfirmedAt ?? x.DeliveredAt ?? x.CreatedAt)
                    .ThenByDescending(x => x.DeliveryId)
                    .First());
    }

    // Load usernames in one query to avoid N+1 lookups for list responses.
    private async Task<Dictionary<int, string>> LoadUsernameMapAsync(List<int> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
            return new Dictionary<int, string>();

        return await _db.Users
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.Username, ct);
    }

    // Resolve the latest receiving report for a delivered order that was confirmed by store.
    private static ReceivingReport? ResolveLatestReceivingReport(Delivery? delivery)
    {
        return delivery?.ReceivingReports
            .OrderByDescending(x => x.ReceivedAt)
            .ThenByDescending(x => x.ReceivingReportId)
            .FirstOrDefault();
    }

    // Resolve the lifecycle end timestamp for final-state supply orders.
    private static DateTime ResolveEndedAt(StoreOrder order, Delivery? delivery)
    {
        if (string.Equals(order.Status, StoreOrderStatus.ReceivedByStore, StringComparison.OrdinalIgnoreCase))
        {
            return ResolveLatestReceivingReport(delivery)?.ReceivedAt
                ?? delivery?.ConfirmedAt
                ?? delivery?.DeliveredAt
                ?? order.DeliveryStatusUpdatedAt
                ?? order.PreparedAt
                ?? order.ForwardedAt
                ?? order.CreatedAt;
        }

        if (string.Equals(order.Status, StoreOrderStatus.Delivered, StringComparison.OrdinalIgnoreCase))
        {
            return delivery?.DeliveredAt
                ?? order.DeliveryStatusUpdatedAt
                ?? order.PreparedAt
                ?? order.ForwardedAt
                ?? order.CreatedAt;
        }

        if (string.Equals(order.Status, StoreOrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            return order.CancelledAt
                ?? order.UpdatedAt
                ?? order.ForwardedAt
                ?? order.CreatedAt;
        }

        return order.UpdatedAt ?? order.CreatedAt;
    }

    // Resolve the lifecycle end note from the final-state source of truth.
    private static string? ResolveEndedNote(StoreOrder order, Delivery? delivery)
    {
        if (string.Equals(order.Status, StoreOrderStatus.ReceivedByStore, StringComparison.OrdinalIgnoreCase))
        {
            return ResolveLatestReceivingReport(delivery)?.Note
                ?? order.DeliveryStatusNote;
        }

        if (string.Equals(order.Status, StoreOrderStatus.Delivered, StringComparison.OrdinalIgnoreCase))
        {
            return order.DeliveryStatusNote;
        }

        if (string.Equals(order.Status, StoreOrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            return order.CancelReason;
        }

        return null;
    }

    // Resolve the user who performed the final lifecycle action when that metadata exists.
    private static int? ResolveEndedByUserId(StoreOrder order, Delivery? delivery)
    {
        if (string.Equals(order.Status, StoreOrderStatus.ReceivedByStore, StringComparison.OrdinalIgnoreCase))
        {
            return ResolveLatestReceivingReport(delivery)?.ReceivedByUserId;
        }

        if (string.Equals(order.Status, StoreOrderStatus.Delivered, StringComparison.OrdinalIgnoreCase))
        {
            return order.DeliveryStatusUpdatedByUserId;
        }

        // Cancelled orders currently do not store CancelledByUserId in schema.
        return null;
    }

    // Normalize the allowed history status filter for processed supply orders.
    private static string NormalizeProcessedHistoryStatus(string? rawStatus)
    {
        var value = (rawStatus ?? "ALL").Trim().ToUpperInvariant();

        return value switch
        {
            "ALL" => "ALL",
            var s when s == StoreOrderStatus.Delivered => StoreOrderStatus.Delivered,
            var s when s == StoreOrderStatus.ReceivedByStore => StoreOrderStatus.ReceivedByStore,
            var s when s == StoreOrderStatus.Cancelled => StoreOrderStatus.Cancelled,
            _ => throw new ArgumentException("Invalid status value. Use ALL / DELIVERED / RECEIVED_BY_STORE / CANCELLED.")
        };
    }

    // Parse SO-000123 or plain numeric search into a store order id when possible.
    private static int? TryParseOrderCodeSearch(string search)
    {
        var value = (search ?? string.Empty).Trim();

        if (value.StartsWith("SO-", StringComparison.OrdinalIgnoreCase))
            value = value[3..];

        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (value.All(char.IsDigit) && int.TryParse(value, out var orderId))
            return orderId;

        return null;
    }

    // Apply stable sorting for the processed supply history list.
    private static List<SupplyProcessedOrderResponse> ApplyProcessedHistorySort(
        List<SupplyProcessedOrderResponse> items,
        string? sortBy,
        string? sortDir)
    {
        var key = (sortBy ?? "endedAt").Trim().ToLowerInvariant();
        var desc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        return key switch
        {
            "storename" => desc
                ? items.OrderByDescending(x => x.StoreName).ThenByDescending(x => x.StoreOrderId).ToList()
                : items.OrderBy(x => x.StoreName).ThenBy(x => x.StoreOrderId).ToList(),

            "status" => desc
                ? items.OrderByDescending(x => x.Status).ThenByDescending(x => x.StoreOrderId).ToList()
                : items.OrderBy(x => x.Status).ThenBy(x => x.StoreOrderId).ToList(),

            "createdat" => desc
                ? items.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.StoreOrderId).ToList()
                : items.OrderBy(x => x.CreatedAt).ThenBy(x => x.StoreOrderId).ToList(),

            "forwardedat" => desc
                ? items.OrderByDescending(x => x.ForwardedAt).ThenByDescending(x => x.StoreOrderId).ToList()
                : items.OrderBy(x => x.ForwardedAt).ThenBy(x => x.StoreOrderId).ToList(),

            "requesteddeliverydate" => desc
                ? items.OrderByDescending(x => x.RequestedDeliveryDate).ThenByDescending(x => x.StoreOrderId).ToList()
                : items.OrderBy(x => x.RequestedDeliveryDate).ThenBy(x => x.StoreOrderId).ToList(),

            _ => desc
                ? items.OrderByDescending(x => x.EndedAt).ThenByDescending(x => x.StoreOrderId).ToList()
                : items.OrderBy(x => x.EndedAt).ThenBy(x => x.StoreOrderId).ToList()
        };
    }

    private async Task<string?> ResolveUsernameAsync(int? userId, CancellationToken ct)
    {
        if (!userId.HasValue)
            return null;

        return await _db.Users
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .Select(x => x.Username)
            .FirstOrDefaultAsync(ct);
    }

    private void RequireOneOf(params string[] roles)
    {
        var currentRole = _current.Role;

        if (roles.Any(r => string.Equals(r, currentRole, StringComparison.OrdinalIgnoreCase)))
            return;

        throw new ForbiddenAccessException("You do not have permission for this action.");
    }

    private static string BuildOrderCode(int storeOrderId)
        => $"SO-{storeOrderId:D6}";
}
