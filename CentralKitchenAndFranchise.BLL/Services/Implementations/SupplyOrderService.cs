using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Extensions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.StoreOrders;
using CentralKitchenAndFranchise.DTO.Responses.StoreOrders;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class SupplyOrderService : ISupplyOrderService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFranchiseAccessService _access;

    public SupplyOrderService(
        AppDbContext db,
        ICurrentUserService current,
        IFranchiseAccessService access)
    {
        _db = db;
        _current = current;
        _access = access;
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

        await EnsureCentralKitchenHasSufficientProductStockAsync(order, ct);
        await EnsureCentralKitchenHasSufficientIngredientStockAsync(order, ct);

        var now = DateTime.UtcNow;
        var currentUserId = _current.UserId;
        var oldStatus = order.Status;

        order.Status = StoreOrderStatus.Preparing;
        order.PreparedAt = now;
        order.PreparedByUserId = currentUserId;
        order.PreparingNote = string.IsNullOrWhiteSpace(request?.PreparingNote)
            ? null
            : request.PreparingNote.Trim();

        await EnsureDeliveryArtifactsAsync(order, ct);

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
            OldDataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                Status = oldStatus
            }),
            NewDataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                order.Status,
                order.PreparedAt,
                order.PreparedByUserId,
                order.PreparingNote
            }),
            Reason = order.PreparingNote,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

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

    private async Task EnsureCentralKitchenHasSufficientProductStockAsync(StoreOrder order, CancellationToken ct)
    {
        var snapshotMap = await LoadProductForwardSnapshotMapAsync(new List<int> { order.StoreOrderId }, ct);
        snapshotMap.TryGetValue(order.StoreOrderId, out var orderSnapshot);

        var resolvedSnapshotMap = ResolveForwardSnapshotByProduct(order, orderSnapshot);

        var snapshotErrors = resolvedSnapshotMap.Values
            .Where(x => !x.HasSnapshot || !x.IsConsistent)
            .Select(x => x.Warning ?? $"Forward snapshot is invalid for ProductId={x.ItemId}.")
            .ToList();

        if (snapshotErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "Cannot prepare delivery because forwarded snapshot is missing or inconsistent. " +
                string.Join("; ", snapshotErrors));
        }

        var requiredMap = resolvedSnapshotMap.Values
            .Where(x => x.ForwardedQuantity > 0)
            .ToDictionary(x => x.ItemId, x => x.ForwardedQuantity);

        if (requiredMap.Count == 0)
            throw new InvalidOperationException("Cannot prepare delivery because there are no forwarded product lines.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var productBatches = (await _db.ProductBatches
                .AsNoTracking()
                .Include(x => x.Product)
                .Where(x =>
                    x.CentralKitchenId == order.Franchise.CentralKitchenId &&
                    x.FranchiseId == null &&
                    requiredMap.Keys.Contains(x.ProductId) &&
                    x.Quantity > 0)
                .ToListAsync(ct))
            .Where(x => x.IsUsableNonExpired(today))
            .ToList();

        var shortages = new List<string>();

        foreach (var entry in requiredMap.OrderBy(x => x.Key))
        {
            var productId = entry.Key;
            var requiredQty = entry.Value;

            var availableQty = productBatches
                .Where(x => x.ProductId == productId)
                .OrderBy(x => x.CalculateExpiredAt() == null)
                .ThenBy(x => x.CalculateExpiredAt())
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.BatchId)
                .Sum(x => x.Quantity);

            if (availableQty >= requiredQty)
                continue;

            var productName = order.Items
                .Where(x => x.ProductId == productId)
                .Select(x => x.Product?.Name)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                ?? $"ProductId={productId}";

            shortages.Add($"{productName}: required={requiredQty}, available={availableQty}");
        }

        if (shortages.Count > 0)
        {
            throw new InvalidOperationException(
                "Insufficient usable central kitchen inventory to prepare this delivery. " +
                string.Join("; ", shortages));
        }
    }

    private async Task EnsureCentralKitchenHasSufficientIngredientStockAsync(StoreOrder order, CancellationToken ct)
    {
        if (order.IngredientItems.Count == 0)
            return;

        var snapshotMap = await LoadIngredientForwardSnapshotMapAsync(new List<int> { order.StoreOrderId }, ct);
        snapshotMap.TryGetValue(order.StoreOrderId, out var orderSnapshot);

        var resolvedSnapshotMap = ResolveForwardSnapshotByIngredient(order, orderSnapshot);

        var snapshotErrors = resolvedSnapshotMap.Values
            .Where(x => !x.HasSnapshot || !x.IsConsistent)
            .Select(x => x.Warning ?? $"Forward snapshot is invalid for IngredientId={x.ItemId}.")
            .ToList();

        if (snapshotErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "Cannot prepare delivery because forwarded ingredient snapshot is missing or inconsistent. " +
                string.Join("; ", snapshotErrors));
        }

        var requiredMap = resolvedSnapshotMap.Values
            .Where(x => x.ForwardedQuantity > 0)
            .ToDictionary(x => x.ItemId, x => x.ForwardedQuantity);

        if (requiredMap.Count == 0)
            return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var availableMap = (await _db.IngredientBatches
                .AsNoTracking()
                .Where(x =>
                    x.CentralKitchenId == order.Franchise.CentralKitchenId &&
                    x.FranchiseId == null &&
                    requiredMap.Keys.Contains(x.IngredientId) &&
                    x.Quantity > 0)
                .Include(x => x.Ingredient)
                .ToListAsync(ct))
            .Where(x => x.IsUsableNonExpired(today))
            .GroupBy(x => x.IngredientId)
            .ToDictionary(x => x.Key, x => x.Sum(i => i.Quantity));

        var insufficient = requiredMap
            .Where(x =>
            {
                var available = availableMap.TryGetValue(x.Key, out var qty) ? qty : 0m;
                return available < x.Value;
            })
            .Select(x =>
            {
                var available = availableMap.TryGetValue(x.Key, out var qty) ? qty : 0m;
                return $"IngredientId={x.Key}: required={x.Value}, available={available}";
            })
            .ToList();

        if (insufficient.Count > 0)
            throw new InvalidOperationException("Central kitchen ingredient stock is insufficient for prepared delivery. " + string.Join("; ", insufficient));
    }

    /// Phase 1:
    /// - 1 StoreOrder -> 1 DeliveryPlan -> 1 Delivery
    /// - sync delivery lines from forward snapshot
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
            (var s, var n) when s == StoreOrderStatus.ForwardedToSupply && n == StoreOrderStatus.Preparing => true,
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
