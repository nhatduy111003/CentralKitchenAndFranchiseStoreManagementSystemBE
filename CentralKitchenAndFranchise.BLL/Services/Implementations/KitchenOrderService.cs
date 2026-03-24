using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Extensions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.StoreOrders;
using CentralKitchenAndFranchise.DTO.Responses.Inventory;
using CentralKitchenAndFranchise.DTO.Responses.StoreOrders;
using Microsoft.EntityFrameworkCore;
using PayOS.Exceptions;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class KitchenOrderService : IKitchenOrderService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFranchiseAccessService _access;
    public KitchenOrderService(AppDbContext db, ICurrentUserService current, IFranchiseAccessService access)
    {
        _db = db;
        _current = current;
        _access = access;
    }

    public async Task<IncomingOrderDetailResponse> GetDetailAsync(int centralKitchenId, int orderId, CancellationToken ct = default)
    {
        await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

        var order = await _db.StoreOrders
            .AsNoTracking()
            .Include(x => x.Franchise)
            .Include(x => x.Items)
                .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x =>
                x.StoreOrderId == orderId &&
                x.Franchise.CentralKitchenId == centralKitchenId, ct);

        if (order is null)
            throw new NotFoundException("Store order not found.");

        var receivedBy = await ResolveUsernameAsync(order.ReceivedByUserId, ct);
        var forwardedBy = await ResolveUsernameAsync(order.ForwardedByUserId, ct);

        var productIds = order.Items
            .Select(x => x.ProductId)
            .Distinct()
            .ToList();

        var availableBatchMap = await LoadCentralKitchenProductBatchMapAsync(
            centralKitchenId,
            productIds,
            ct);

        var availableQtyMap = availableBatchMap
            .ToDictionary(x => x.Key, x => x.Value.Sum(b => b.Quantity));

        var snapshotMap = await LoadForwardSnapshotMapAsync(order.StoreOrderId, ct);

        var items = order.Items
            .OrderBy(i => i.ProductId)
            .Select(i =>
            {
                var availableQty = GetTotalQuantity(availableQtyMap, i.ProductId);
                snapshotMap.TryGetValue(i.ProductId, out var snapshot);
                var resolvedSnapshot = ResolveForwardSnapshot(order.Status, i.ProductId, i.Quantity, snapshot);

                return new IncomingOrderDetailItemResponse
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? "(unknown)",
                    Sku = i.Product?.Sku,
                    Unit = i.Product?.Unit ?? "",
                    Quantity = i.Quantity,
                    ProductStatus = i.Product?.Status,

                    ForwardedQuantity = resolvedSnapshot.ForwardedQuantity,
                    DroppedQuantity = resolvedSnapshot.DroppedQuantity,
                    IsDroppedFromForward = resolvedSnapshot.IsDropped,
                    DropReason = resolvedSnapshot.DropReason,

                    HasForwardSnapshot = resolvedSnapshot.HasSnapshot,
                    IsForwardSnapshotConsistent = resolvedSnapshot.IsConsistent,
                    ForwardSnapshotWarning = resolvedSnapshot.Warning,
                    RawForwardSnapshotRequestedQuantity = resolvedSnapshot.RawRequestedQuantity,
                    RawForwardSnapshotForwardedQuantity = resolvedSnapshot.RawForwardedQuantity,
                    RawForwardSnapshotDroppedQuantity = resolvedSnapshot.RawDroppedQuantity,

                    AvailableInCentralKitchenQuantity = availableQty,
                    IsSufficientInCentralKitchen = availableQty >= i.Quantity,
                    AvailableCentralKitchenBatches = GetBatchList(availableBatchMap, i.ProductId)
                };
            })
            .ToList();

        return new IncomingOrderDetailResponse
        {
            StoreOrderId = order.StoreOrderId,
            OrderCode = BuildOrderCode(order.StoreOrderId),
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            RequestedDeliveryDate = order.OrderDate,
            StoreNote = order.StoreNote,
            StoreId = order.FranchiseId,
            StoreName = order.Franchise.Name,
            StoreAddress = order.Franchise.Address,
            TotalItems = order.Items.Count,
            TotalQuantity = order.Items.Sum(i => i.Quantity),
            ForwardedTotalItems = items.Count(x => x.ForwardedQuantity > 0),
            ForwardedTotalQuantity = items.Sum(x => x.ForwardedQuantity),
            DroppedTotalItems = items.Count(x => x.IsDroppedFromForward),
            DroppedTotalQuantity = items.Sum(x => x.DroppedQuantity),
            ReceivedAt = order.ReceivedAt,
            ReceivedBy = receivedBy,
            ForwardedAt = order.ForwardedAt,
            ForwardedBy = forwardedBy,
            ProcessingNote = order.ProcessingNote,
            ForwardNote = order.ForwardNote,
            Items = items
        };
    }

    public async Task<OrderWorkflowActionResponse> ReceiveAsync(
        int centralKitchenId,
        int orderId,
        ReceiveIncomingOrderRequest request,
        CancellationToken ct = default)
    {
        await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

        var order = await LoadManagedOrderAsync(centralKitchenId, orderId, ct);

        if (!string.Equals(order.Status, StoreOrderStatus.Locked, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Only LOCKED orders can be received by kitchen.");

        var now = DateTime.UtcNow;
        var currentUserId = _current.UserId;

        var oldStatus = order.Status;

        order.Status = StoreOrderStatus.ReceivedByKitchen;
        order.ReceivedAt = now;
        order.ReceivedByUserId = currentUserId;
        order.ReceiveNote = request.ReceiveNote;

        await AddHistoryAsync(
            order.StoreOrderId,
            StoreOrderHistoryActions.OrderReceivedByKitchen,
            "Kitchen đã tiếp nhận đơn",
            oldStatus,
            order.Status,
            request.ReceiveNote,
            currentUserId,
            now,
            ct);

        await AddAuditLogAsync("STORE_ORDER_RECEIVED_BY_KITCHEN", order.StoreOrderId, oldStatus, order.Status, request.ReceiveNote, now, ct);

        await _db.SaveChangesAsync(ct);

        return new OrderWorkflowActionResponse
        {
            StoreOrderId = order.StoreOrderId,
            Status = order.Status,
            ReceivedAt = order.ReceivedAt,
            ReceivedBy = await ResolveUsernameAsync(order.ReceivedByUserId, ct),
            Message = "Kitchen received the order successfully."
        };
    }

    public async Task<OrderWorkflowActionResponse> UpdateProcessingNoteAsync(
        int centralKitchenId,
        int orderId,
        UpdateProcessingNoteRequest request,
        CancellationToken ct = default)
    {
        await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

        var order = await LoadManagedOrderAsync(centralKitchenId, orderId, ct);

        var allowed = new[]
        {
            StoreOrderStatus.Locked,
            StoreOrderStatus.ReceivedByKitchen,
            StoreOrderStatus.ForwardedToSupply
        };

        if (!allowed.Contains(order.Status, StringComparer.OrdinalIgnoreCase))
            throw new BadRequestException("Processing note can only be updated on active kitchen/supply workflow orders.");

        var now = DateTime.UtcNow;
        var currentUserId = _current.UserId;

        order.ProcessingNote = request.ProcessingNote.Trim();
        order.ProcessingNoteUpdatedAt = now;
        order.ProcessingNoteUpdatedByUserId = currentUserId;

        await AddHistoryAsync(
            order.StoreOrderId,
            StoreOrderHistoryActions.ProcessingNoteUpdated,
            "Cập nhật ghi chú xử lý",
            order.Status,
            order.Status,
            order.ProcessingNote,
            currentUserId,
            now,
            ct);

        await AddAuditLogAsync("STORE_ORDER_PROCESSING_NOTE_UPDATED", order.StoreOrderId, order.Status, order.Status, order.ProcessingNote, now, ct);

        await _db.SaveChangesAsync(ct);

        return new OrderWorkflowActionResponse
        {
            StoreOrderId = order.StoreOrderId,
            Status = order.Status,
            ProcessingNote = order.ProcessingNote,
            UpdatedAt = order.ProcessingNoteUpdatedAt,
            UpdatedBy = await ResolveUsernameAsync(order.ProcessingNoteUpdatedByUserId, ct),
            Message = "Processing note updated successfully."
        };
    }

    public async Task<OrderWorkflowActionResponse> ForwardToSupplyAsync(
    int centralKitchenId,
    int orderId,
    ForwardToSupplyRequest request,
    CancellationToken ct = default)
    {
        await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

        var order = await LoadManagedOrderAsync(centralKitchenId, orderId, ct);

        if (!string.Equals(order.Status, StoreOrderStatus.ReceivedByKitchen, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Only orders received by kitchen can be forwarded to supply.");

        var forwardPlan = await EvaluateForwardPlanAsync(order, ct);

        if (!forwardPlan.Any(x => x.ForwardedQuantity > 0))
            throw new BadRequestException("No order lines can be forwarded because none of them have sufficient central kitchen stock.");

        await UpsertDeliveryArtifactsFromForwardPlanAsync(order, forwardPlan, ct);

        var now = DateTime.UtcNow;
        var currentUserId = _current.UserId;
        var oldStatus = order.Status;

        order.Status = StoreOrderStatus.ForwardedToSupply;
        order.ForwardedAt = now;
        order.ForwardedByUserId = currentUserId;
        order.ForwardNote = request.ForwardNote;

        await AddHistoryAsync(
            order.StoreOrderId,
            StoreOrderHistoryActions.OrderForwardedToSupply,
            "Kitchen đã chuyển đơn sang Supply",
            oldStatus,
            order.Status,
            request.ForwardNote,
            currentUserId,
            now,
            ct);

        await AddAuditLogAsync("STORE_ORDER_FORWARDED_TO_SUPPLY", order.StoreOrderId, oldStatus, order.Status, request.ForwardNote, now, ct);

        await _db.SaveChangesAsync(ct);

        return new OrderWorkflowActionResponse
        {
            StoreOrderId = order.StoreOrderId,
            Status = order.Status,
            ForwardedAt = order.ForwardedAt,
            ForwardedBy = await ResolveUsernameAsync(order.ForwardedByUserId, ct),
            ForwardNote = order.ForwardNote,
            ForwardedTotalItems = forwardPlan.Count(x => x.ForwardedQuantity > 0),
            ForwardedTotalQuantity = forwardPlan.Sum(x => x.ForwardedQuantity),
            DroppedTotalItems = forwardPlan.Count(x => x.IsDropped),
            DroppedTotalQuantity = forwardPlan.Sum(x => x.DroppedQuantity),
            ForwardResultItems = forwardPlan
                .OrderBy(x => x.ProductId)
                .Select(x => new OrderForwardResultItemResponse
                {
                    ProductId = x.ProductId,
                    ProductName = x.ProductName,
                    Sku = x.Sku,
                    Unit = x.Unit,
                    RequestedQuantity = x.RequestedQuantity,
                    ForwardedQuantity = x.ForwardedQuantity,
                    DroppedQuantity = x.DroppedQuantity,
                    IsDropped = x.IsDropped,
                    DropReason = x.DropReason
                })
                .ToList(),
            Message = "Order forwarded to supply successfully."
        };
    }

    public async Task<List<StoreOrderHistoryResponse>> GetHistoryAsync(int centralKitchenId, int orderId, CancellationToken ct = default)
    {
        await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

        var exists = await _db.StoreOrders
            .AsNoTracking()
            .AnyAsync(x => x.StoreOrderId == orderId && x.Franchise.CentralKitchenId == centralKitchenId, ct);

        if (!exists)
            throw new NotFoundException("Store order not found.");

        var histories = await _db.Set<StoreOrderHistory>()
            .AsNoTracking()
            .Where(x => x.StoreOrderId == orderId)
            .OrderByDescending(x => x.PerformedAt)
            .ToListAsync(ct);

        var userIds = histories
            .Where(x => x.PerformedByUserId.HasValue)
            .Select(x => x.PerformedByUserId!.Value)
            .Distinct()
            .ToList();

        var users = await _db.Users
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.Username, ct);

        return histories.Select(x => new StoreOrderHistoryResponse
        {
            HistoryId = x.StoreOrderHistoryId,
            StoreOrderId = x.StoreOrderId,
            ActionType = x.ActionType,
            ActionLabel = x.ActionLabel,
            OldStatus = x.OldStatus,
            NewStatus = x.NewStatus,
            Note = x.Note,
            PerformedAt = x.PerformedAt,
            PerformedBy = x.PerformedByUserId.HasValue && users.TryGetValue(x.PerformedByUserId.Value, out var username)
                ? username
                : null
        }).ToList();
    }

    private async Task<StoreOrder> LoadManagedOrderAsync(int centralKitchenId, int orderId, CancellationToken ct)
    {
        var order = await _db.StoreOrders
            .Include(x => x.Franchise)
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x =>
                x.StoreOrderId == orderId &&
                x.Franchise.CentralKitchenId == centralKitchenId, ct);

        if (order is null)
            throw new NotFoundException("Store order not found.");

        return order;
    }

    //helpers
    private sealed class ForwardPlanLine
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = default!;
        public string? Sku { get; set; }
        public string Unit { get; set; } = default!;

        public decimal RequestedQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public decimal ForwardedQuantity { get; set; }

        public decimal DroppedQuantity => RequestedQuantity - ForwardedQuantity;
        public bool IsDropped { get; set; }
        public string? DropReason { get; set; }
    }

    private sealed class ForwardSnapshotLine
    {
        public int ProductId { get; set; }
        public decimal RequestedQuantity { get; set; }
        public decimal ForwardedQuantity { get; set; }
        public decimal DroppedQuantity { get; set; }
        public bool IsDropped { get; set; }
        public string? DropReason { get; set; }
    }

    private sealed class ResolvedForwardSnapshotLine
    {
        public bool HasSnapshot { get; set; }
        public bool IsConsistent { get; set; }
        public string? Warning { get; set; }

        public decimal RawRequestedQuantity { get; set; }
        public decimal RawForwardedQuantity { get; set; }
        public decimal RawDroppedQuantity { get; set; }

        public decimal ForwardedQuantity { get; set; }
        public decimal DroppedQuantity { get; set; }
        public bool IsDropped { get; set; }
        public string? DropReason { get; set; }
    }

    private async Task<List<ForwardPlanLine>> EvaluateForwardPlanAsync(StoreOrder order, CancellationToken ct)
    {
        if (order.Franchise is null)
            throw new InvalidOperationException("Store order franchise context is missing.");

        if (order.Items is null || order.Items.Count == 0)
            throw new BadRequestException("Cannot forward an empty order to supply.");

        var requiredMap = order.Items
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

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
            .Where(x => IsUsableNonExpiredProductBatch(x, today))
            .ToList();

        return order.Items
            .OrderBy(x => x.ProductId)
            .Select(item =>
            {
                var availableQty = productBatches
                    .Where(x => x.ProductId == item.ProductId)
                    .OrderBy(x => x.CalculateExpiredAt() == null)
                    .ThenBy(x => x.CalculateExpiredAt())
                    .ThenBy(x => x.CreatedAt)
                    .ThenBy(x => x.BatchId)
                    .Sum(x => x.Quantity);

                var isFulfillable = availableQty >= item.Quantity;

                return new ForwardPlanLine
                {
                    ProductId = item.ProductId,
                    ProductName = item.Product?.Name ?? "(unknown)",
                    Sku = item.Product?.Sku,
                    Unit = item.Product?.Unit ?? "",
                    RequestedQuantity = item.Quantity,
                    AvailableQuantity = availableQty,
                    ForwardedQuantity = isFulfillable ? item.Quantity : 0m,
                    IsDropped = !isFulfillable,
                    DropReason = isFulfillable
                        ? null
                        : $"Insufficient central kitchen inventory. Required={item.Quantity}, Available={availableQty}."
                };
            })
            .ToList();
    }

    private async Task UpsertDeliveryArtifactsFromForwardPlanAsync(
        StoreOrder order,
        IReadOnlyCollection<ForwardPlanLine> forwardPlan,
        CancellationToken ct)
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

        var existingDelivery = await _db.Deliveries
            .Include(x => x.ProductItems)
            .FirstOrDefaultAsync(x => x.DeliveryPlanId == existingPlan.DeliveryPlanId, ct);

        if (existingDelivery is null)
        {
            existingDelivery = new Delivery
            {
                DeliveryPlanId = existingPlan.DeliveryPlanId,
                FromCentralKitchenId = order.Franchise.CentralKitchenId,
                Status = DeliveryStatus.Created,
                CreatedAt = DateTime.UtcNow
            };

            _db.Deliveries.Add(existingDelivery);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            existingDelivery.FromCentralKitchenId = order.Franchise.CentralKitchenId;
        }

        var existingItemMap = existingDelivery.ProductItems
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var line in forwardPlan)
        {
            if (existingItemMap.TryGetValue(line.ProductId, out var deliveryLine))
            {
                deliveryLine.Quantity = line.ForwardedQuantity;
                deliveryLine.RequestedQuantity = line.RequestedQuantity;
                deliveryLine.IsDropped = line.IsDropped;
                deliveryLine.DropReason = line.DropReason;
            }
            else
            {
                existingDelivery.ProductItems.Add(new DeliveryProductItem
                {
                    ProductId = line.ProductId,
                    Quantity = line.ForwardedQuantity,
                    RequestedQuantity = line.RequestedQuantity,
                    IsDropped = line.IsDropped,
                    DropReason = line.DropReason
                });
            }
        }

        var validProductIds = forwardPlan.Select(x => x.ProductId).ToHashSet();

        var orphanLines = existingDelivery.ProductItems
            .Where(x => !validProductIds.Contains(x.ProductId))
            .ToList();

        if (orphanLines.Count > 0)
        {
            _db.DeliveryProductItems.RemoveRange(orphanLines);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<int, ForwardSnapshotLine>> LoadForwardSnapshotMapAsync(int orderId, CancellationToken ct)
    {
        var lines = await _db.DeliveryProductItems
            .AsNoTracking()
            .Include(x => x.Delivery)
                .ThenInclude(x => x.DeliveryPlan)
            .Where(x => x.Delivery.DeliveryPlan.StoreOrderId == orderId)
            .ToListAsync(ct);

        return lines
            .GroupBy(x => x.ProductId)
            .Select(g =>
            {
                var requested = g.Sum(x => x.RequestedQuantity > 0 ? x.RequestedQuantity : x.Quantity);
                var forwarded = g.Sum(x => x.Quantity);
                var reasons = g
                    .Where(x => !string.IsNullOrWhiteSpace(x.DropReason))
                    .Select(x => x.DropReason!.Trim())
                    .Distinct()
                    .ToList();

                return new ForwardSnapshotLine
                {
                    ProductId = g.Key,
                    RequestedQuantity = requested,
                    ForwardedQuantity = forwarded,
                    DroppedQuantity = Math.Max(requested - forwarded, 0m),
                    IsDropped = g.Any(x => x.IsDropped),
                    DropReason = reasons.Count == 0 ? null : string.Join(" | ", reasons)
                };
            })
            .ToDictionary(x => x.ProductId, x => x);
    }

    private async Task<Dictionary<int, List<InventoryBatchQuantityResponse>>> LoadCentralKitchenProductBatchMapAsync(
    int centralKitchenId,
    List<int> productIds,
    CancellationToken ct)
    {
        if (productIds.Count == 0)
            return new();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var batches = (await _db.ProductBatches
                .AsNoTracking()
                .Include(x => x.Product)
                .Where(x =>
                    x.CentralKitchenId == centralKitchenId &&
                    x.FranchiseId == null &&
                    productIds.Contains(x.ProductId) &&
                    x.Quantity > 0)
                .ToListAsync(ct))
            .Where(x => IsUsableNonExpiredProductBatch(x, today))
            .ToList();

        return batches
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderBy(x => x.CalculateExpiredAt() == null)
                    .ThenBy(x => x.CalculateExpiredAt())
                    .ThenBy(x => x.CreatedAt)
                    .ThenBy(x => x.BatchId)
                    .Select(x => new InventoryBatchQuantityResponse
                    {
                        BatchId = x.BatchId,
                        BatchCode = x.BatchCode,
                        Quantity = x.Quantity,
                        CreatedAt = x.CreatedAt,
                        ExpiredAt = x.CalculateExpiredAt()
                    })
                    .ToList());
    }

    private static bool IsUsableNonExpiredProductBatch(ProductBatch batch, DateOnly today)
    {
        if (batch.Quantity <= 0)
            return false;

        var expiredAt = batch.CalculateExpiredAt();
        return expiredAt is null || expiredAt.Value >= today;
    }

    private static bool ShouldExposeForwardSnapshot(string status)
        => status is StoreOrderStatus.ForwardedToSupply
            or StoreOrderStatus.Preparing
            or StoreOrderStatus.ReadyToDeliver
            or StoreOrderStatus.InTransit
            or StoreOrderStatus.Delivered
            or StoreOrderStatus.ReceivedByStore;

    private static ResolvedForwardSnapshotLine ResolveForwardSnapshot(
        string orderStatus,
        int productId,
        decimal orderQuantity,
        ForwardSnapshotLine? snapshot)
    {
        var shouldExpose = ShouldExposeForwardSnapshot(orderStatus);

        if (snapshot is null)
        {
            return new ResolvedForwardSnapshotLine
            {
                HasSnapshot = false,
                IsConsistent = !shouldExpose,
                Warning = shouldExpose
                    ? $"Forward snapshot is missing for ProductId={productId} while order status is {orderStatus}."
                    : null
            };
        }

        var resolved = new ResolvedForwardSnapshotLine
        {
            HasSnapshot = true,
            RawRequestedQuantity = snapshot.RequestedQuantity,
            RawForwardedQuantity = snapshot.ForwardedQuantity,
            RawDroppedQuantity = snapshot.DroppedQuantity
        };

        // Có snapshot trước khi order tới stage forward => stale artifact / dữ liệu cũ
        if (!shouldExpose)
        {
            resolved.IsConsistent = false;
            resolved.Warning = $"Forward snapshot already exists for ProductId={productId} while order status is {orderStatus}.";
            return resolved;
        }

        var expectedDropped = Math.Max(snapshot.RequestedQuantity - snapshot.ForwardedQuantity, 0m);
        string? warning = null;

        if (snapshot.RequestedQuantity <= 0)
        {
            warning = $"Forward snapshot requested quantity is invalid for ProductId={productId}.";
        }
        else if (snapshot.RequestedQuantity != orderQuantity)
        {
            warning = $"Forward snapshot requested quantity ({snapshot.RequestedQuantity}) does not match current order quantity ({orderQuantity}) for ProductId={productId}.";
        }
        else if (snapshot.ForwardedQuantity > snapshot.RequestedQuantity)
        {
            warning = $"Forward snapshot forwarded quantity ({snapshot.ForwardedQuantity}) exceeds requested quantity ({snapshot.RequestedQuantity}) for ProductId={productId}.";
        }
        else if (snapshot.ForwardedQuantity > orderQuantity)
        {
            warning = $"Forward snapshot forwarded quantity ({snapshot.ForwardedQuantity}) exceeds current order quantity ({orderQuantity}) for ProductId={productId}.";
        }
        else if (snapshot.DroppedQuantity != expectedDropped)
        {
            warning = $"Forward snapshot dropped quantity ({snapshot.DroppedQuantity}) does not match expected dropped quantity ({expectedDropped}) for ProductId={productId}.";
        }

        if (warning is not null)
        {
            resolved.IsConsistent = false;
            resolved.Warning = warning;
            return resolved;
        }

        resolved.IsConsistent = true;
        resolved.ForwardedQuantity = snapshot.ForwardedQuantity;
        resolved.DroppedQuantity = snapshot.DroppedQuantity;
        resolved.IsDropped = snapshot.IsDropped;
        resolved.DropReason = snapshot.DropReason;
        return resolved;
    }

    private static decimal GetTotalQuantity(Dictionary<int, decimal> map, int itemId)
        => map.TryGetValue(itemId, out var value) ? value : 0m;

    private static List<InventoryBatchQuantityResponse> GetBatchList(
        Dictionary<int, List<InventoryBatchQuantityResponse>> map,
        int itemId)
        => map.TryGetValue(itemId, out var value)
            ? value
            : new List<InventoryBatchQuantityResponse>();

    private async Task AddHistoryAsync(
        int storeOrderId,
        string actionType,
        string actionLabel,
        string? oldStatus,
        string? newStatus,
        string? note,
        int? performedByUserId,
        DateTime performedAt,
        CancellationToken ct)
    {
        _db.Set<StoreOrderHistory>().Add(new StoreOrderHistory
        {
            StoreOrderId = storeOrderId,
            ActionType = actionType,
            ActionLabel = actionLabel,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Note = note,
            PerformedByUserId = performedByUserId,
            PerformedAt = performedAt
        });

        await Task.CompletedTask;
    }

    private async Task AddAuditLogAsync(
        string action,
        int entityId,
        string? oldStatus,
        string? newStatus,
        string? note,
        DateTime now,
        CancellationToken ct)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = _current.UserId,
            Action = action,
            EntityName = "StoreOrder",
            EntityId = entityId,
            OldDataJson = oldStatus is null ? null : $"{{\"status\":\"{oldStatus}\"}}",
            NewDataJson = $"{{\"status\":\"{newStatus}\",\"note\":{(note is null ? "null" : $"\"{note}\"")}}}",
            CreatedAt = now
        });

        await Task.CompletedTask;
    }

    private async Task<string?> ResolveUsernameAsync(int? userId, CancellationToken ct)
    {
        if (!userId.HasValue)
            return null;

        var user = await _db.Users
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .Select(x => x.Username)
            .FirstOrDefaultAsync(ct);

        return user;
    }

    private static string BuildOrderCode(int storeOrderId)
        => $"SO-{storeOrderId:D6}";


}