using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Extensions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.StoreOrders;
using CentralKitchenAndFranchise.DTO.Responses.Inventory;
using CentralKitchenAndFranchise.DTO.Responses.StoreOrders;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

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
            .Include(x => x.IngredientItems)
                .ThenInclude(x => x.Ingredient)
            .FirstOrDefaultAsync(x =>
                x.StoreOrderId == orderId &&
                x.Franchise.CentralKitchenId == centralKitchenId, ct);

        if (order is null)
            throw new KeyNotFoundException("Store order not found.");

        var receivedBy = await ResolveUsernameAsync(order.ReceivedByUserId, ct);
        var forwardedBy = await ResolveUsernameAsync(order.ForwardedByUserId, ct);

        var productIds = order.Items.Select(x => x.ProductId).Distinct().ToList();
        var ingredientIds = order.IngredientItems.Select(x => x.IngredientId).Distinct().ToList();

        var availableProductBatchMap = await LoadCentralKitchenProductBatchMapAsync(centralKitchenId, productIds, ct);
        var availableIngredientBatchMap = await LoadCentralKitchenIngredientBatchMapAsync(centralKitchenId, ingredientIds, ct);

        var availableProductQtyMap = availableProductBatchMap.ToDictionary(x => x.Key, x => x.Value.Sum(b => b.Quantity));
        var availableIngredientQtyMap = availableIngredientBatchMap.ToDictionary(x => x.Key, x => x.Value.Sum(b => b.Quantity));

        var productSnapshotMap = await LoadProductForwardSnapshotMapAsync(order.StoreOrderId, ct);
        var ingredientSnapshotMap = await LoadIngredientForwardSnapshotMapAsync(order.StoreOrderId, ct);

        var resolvedProductSnapshotMap = ResolveForwardSnapshotByProduct(order, productSnapshotMap);
        var resolvedIngredientSnapshotMap = ResolveForwardSnapshotByIngredient(order, ingredientSnapshotMap);

        var productItems = order.Items
            .OrderBy(x => x.ProductId)
            .Select(x =>
            {
                resolvedProductSnapshotMap.TryGetValue(x.ProductId, out var resolved);
                var availableQty = availableProductQtyMap.TryGetValue(x.ProductId, out var qty) ? qty : 0m;

                return new IncomingOrderDetailItemResponse
                {
                    ProductId = x.ProductId,
                    ProductName = x.Product?.Name ?? "(unknown)",
                    Sku = x.Product?.Sku,
                    Unit = x.Product?.Unit ?? "",
                    Quantity = x.Quantity,
                    ProductStatus = x.Product?.Status,

                    ForwardedQuantity = resolved?.ForwardedQuantity ?? 0m,
                    DroppedQuantity = resolved?.DroppedQuantity ?? 0m,
                    IsDroppedFromForward = resolved?.IsDropped ?? false,
                    DropReason = resolved?.DropReason,

                    HasForwardSnapshot = resolved?.HasSnapshot ?? false,
                    IsForwardSnapshotConsistent = resolved?.IsConsistent ?? false,
                    ForwardSnapshotWarning = resolved?.Warning,
                    RawForwardSnapshotRequestedQuantity = resolved?.RawRequestedQuantity ?? 0m,
                    RawForwardSnapshotForwardedQuantity = resolved?.RawForwardedQuantity ?? 0m,
                    RawForwardSnapshotDroppedQuantity = resolved?.RawDroppedQuantity ?? 0m,

                    AvailableInCentralKitchenQuantity = availableQty,
                    IsSufficientInCentralKitchen = availableQty >= x.Quantity,
                    AvailableCentralKitchenBatches = availableProductBatchMap.TryGetValue(x.ProductId, out var batches)
                        ? batches
                        : new List<InventoryBatchQuantityResponse>()
                };
            })
            .ToList();

        var ingredientItems = order.IngredientItems
            .OrderBy(x => x.IngredientId)
            .Select(x =>
            {
                resolvedIngredientSnapshotMap.TryGetValue(x.IngredientId, out var resolved);
                var availableQty = availableIngredientQtyMap.TryGetValue(x.IngredientId, out var qty) ? qty : 0m;

                return new IncomingOrderDetailIngredientItemResponse
                {
                    IngredientId = x.IngredientId,
                    IngredientName = x.Ingredient?.Name ?? "(unknown)",
                    Unit = x.Ingredient?.Unit ?? "",
                    Quantity = x.Quantity,
                    IngredientStatus = x.Ingredient?.Status,

                    ForwardedQuantity = resolved?.ForwardedQuantity ?? 0m,
                    DroppedQuantity = resolved?.DroppedQuantity ?? 0m,
                    IsDroppedFromForward = resolved?.IsDropped ?? false,
                    DropReason = resolved?.DropReason,

                    HasForwardSnapshot = resolved?.HasSnapshot ?? false,
                    IsForwardSnapshotConsistent = resolved?.IsConsistent ?? false,
                    ForwardSnapshotWarning = resolved?.Warning,
                    RawForwardSnapshotRequestedQuantity = resolved?.RawRequestedQuantity ?? 0m,
                    RawForwardSnapshotForwardedQuantity = resolved?.RawForwardedQuantity ?? 0m,
                    RawForwardSnapshotDroppedQuantity = resolved?.RawDroppedQuantity ?? 0m,

                    AvailableInCentralKitchenQuantity = availableQty,
                    IsSufficientInCentralKitchen = availableQty >= x.Quantity,
                    AvailableCentralKitchenBatches = availableIngredientBatchMap.TryGetValue(x.IngredientId, out var batches)
                        ? batches
                        : new List<InventoryBatchQuantityResponse>()
                };
            })
            .ToList();

        return new IncomingOrderDetailResponse
        {
            StoreOrderId = order.StoreOrderId,
            OrderCode = BuildOrderCode(order.StoreOrderId),
            Status = order.Status,
            RequestedDeliveryDate = order.OrderDate,
            CreatedAt = order.CreatedAt,
            StoreNote = order.StoreNote,
            StoreId = order.FranchiseId,
            StoreName = order.Franchise?.Name ?? "(unknown)",
            StoreAddress = order.Franchise?.Address,

            TotalItems = order.Items.Count + order.IngredientItems.Count,
            TotalQuantity = order.Items.Sum(x => x.Quantity) + order.IngredientItems.Sum(x => x.Quantity),

            ForwardedTotalItems = productItems.Count(x => x.ForwardedQuantity > 0) + ingredientItems.Count(x => x.ForwardedQuantity > 0),
            ForwardedTotalQuantity = productItems.Sum(x => x.ForwardedQuantity) + ingredientItems.Sum(x => x.ForwardedQuantity),
            DroppedTotalItems = productItems.Count(x => x.IsDroppedFromForward) + ingredientItems.Count(x => x.IsDroppedFromForward),
            DroppedTotalQuantity = productItems.Sum(x => x.DroppedQuantity) + ingredientItems.Sum(x => x.DroppedQuantity),

            ReceivedAt = order.ReceivedAt,
            ReceivedBy = receivedBy,
            ForwardedAt = order.ForwardedAt,
            ForwardedBy = forwardedBy,
            ProcessingNote = order.ProcessingNote,
            ForwardNote = order.ForwardNote,

            Items = productItems,
            IngredientItems = ingredientItems
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
            throw new BadHttpRequestException("Only LOCKED orders can be received by kitchen.");

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
            throw new BadHttpRequestException("Processing note can only be updated on active kitchen/supply workflow orders.");

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
            throw new BadHttpRequestException("Only orders received by kitchen can be forwarded to supply.");

        var productForwardPlan = await EvaluateProductForwardPlanAsync(order, ct);
        var ingredientForwardPlan = await EvaluateIngredientForwardPlanAsync(order, ct);

        var hasAnyForwardedLine =
            productForwardPlan.Any(x => x.ForwardedQuantity > 0) ||
            ingredientForwardPlan.Any(x => x.ForwardedQuantity > 0);

        if (!hasAnyForwardedLine)
            throw new BadHttpRequestException("No order lines can be forwarded because none of them have sufficient central kitchen stock.");

        await UpsertDeliveryArtifactsFromForwardPlanAsync(order, productForwardPlan, ingredientForwardPlan, ct);

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

            ForwardedTotalItems =
                productForwardPlan.Count(x => x.ForwardedQuantity > 0) +
                ingredientForwardPlan.Count(x => x.ForwardedQuantity > 0),

            ForwardedTotalQuantity =
                productForwardPlan.Sum(x => x.ForwardedQuantity) +
                ingredientForwardPlan.Sum(x => x.ForwardedQuantity),

            DroppedTotalItems =
                productForwardPlan.Count(x => x.IsDropped) +
                ingredientForwardPlan.Count(x => x.IsDropped),

            DroppedTotalQuantity =
                productForwardPlan.Sum(x => x.DroppedQuantity) +
                ingredientForwardPlan.Sum(x => x.DroppedQuantity),

            ForwardResultItems = productForwardPlan
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

            IngredientForwardResultItems = ingredientForwardPlan
                .OrderBy(x => x.IngredientId)
                .Select(x => new OrderForwardResultIngredientItemResponse
                {
                    IngredientId = x.IngredientId,
                    IngredientName = x.IngredientName,
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
            throw new KeyNotFoundException("Store order not found.");

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
            .Include(x => x.IngredientItems)
                .ThenInclude(x => x.Ingredient)
            .FirstOrDefaultAsync(x =>
                x.StoreOrderId == orderId &&
                x.Franchise.CentralKitchenId == centralKitchenId, ct);

        if (order is null)
            throw new KeyNotFoundException("Store order not found.");

        return order;
    }

    //helpers
    private sealed class ProductForwardPlanLine
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

    private sealed class IngredientForwardPlanLine
    {
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = default!;
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
        public int ItemId { get; set; }
        public decimal RequestedQuantity { get; set; }
        public decimal ForwardedQuantity { get; set; }
        public decimal DroppedQuantity { get; set; }
        public bool IsDropped { get; set; }
        public string? DropReason { get; set; }
    }

    private async Task<List<ProductForwardPlanLine>> EvaluateProductForwardPlanAsync(StoreOrder order, CancellationToken ct)
    {
        if (order.Franchise is null)
            throw new InvalidOperationException("Store order franchise context is missing.");

        var requiredMap = order.Items
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        if (requiredMap.Count == 0)
            return new List<ProductForwardPlanLine>();

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

                return new ProductForwardPlanLine
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

    private async Task<List<IngredientForwardPlanLine>> EvaluateIngredientForwardPlanAsync(StoreOrder order, CancellationToken ct)
    {
        if (order.Franchise is null)
            throw new InvalidOperationException("Store order franchise context is missing.");

        var requiredMap = order.IngredientItems
            .GroupBy(x => x.IngredientId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        if (requiredMap.Count == 0)
            return new List<IngredientForwardPlanLine>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var ingredientBatches = (await _db.IngredientBatches
                .AsNoTracking()
                .Include(x => x.Ingredient)
                .Where(x =>
                    x.CentralKitchenId == order.Franchise.CentralKitchenId &&
                    x.FranchiseId == null &&
                    requiredMap.Keys.Contains(x.IngredientId) &&
                    x.Quantity > 0)
                .ToListAsync(ct))
            .Where(x => x.IsUsableNonExpired(today))
            .ToList();

        return order.IngredientItems
            .OrderBy(x => x.IngredientId)
            .Select(item =>
            {
                var availableQty = ingredientBatches
                    .Where(x => x.IngredientId == item.IngredientId)
                    .OrderBy(x => x.CalculateExpiredAt() == null)
                    .ThenBy(x => x.CalculateExpiredAt())
                    .ThenBy(x => x.CreatedAt)
                    .ThenBy(x => x.BatchId)
                    .Sum(x => x.Quantity);

                var isFulfillable = availableQty >= item.Quantity;

                return new IngredientForwardPlanLine
                {
                    IngredientId = item.IngredientId,
                    IngredientName = item.Ingredient?.Name ?? "(unknown)",
                    Unit = item.Ingredient?.Unit ?? "",
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
        IReadOnlyCollection<ProductForwardPlanLine> productForwardPlan,
        IReadOnlyCollection<IngredientForwardPlanLine> ingredientForwardPlan,
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
            .Include(x => x.IngredientItems)
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

        var existingProductMap = existingDelivery.ProductItems
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var line in productForwardPlan)
        {
            if (existingProductMap.TryGetValue(line.ProductId, out var deliveryLine))
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

        var validProductIds = productForwardPlan.Select(x => x.ProductId).ToHashSet();
        var orphanProductLines = existingDelivery.ProductItems
            .Where(x => !validProductIds.Contains(x.ProductId))
            .ToList();

        if (orphanProductLines.Count > 0)
            _db.DeliveryProductItems.RemoveRange(orphanProductLines);

        var existingIngredientMap = existingDelivery.IngredientItems
            .GroupBy(x => x.IngredientId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var line in ingredientForwardPlan)
        {
            if (existingIngredientMap.TryGetValue(line.IngredientId, out var deliveryLine))
            {
                deliveryLine.Quantity = line.ForwardedQuantity;
                deliveryLine.RequestedQuantity = line.RequestedQuantity;
                deliveryLine.IsDropped = line.IsDropped;
                deliveryLine.DropReason = line.DropReason;
            }
            else
            {
                existingDelivery.IngredientItems.Add(new DeliveryIngredientItem
                {
                    IngredientId = line.IngredientId,
                    Quantity = line.ForwardedQuantity,
                    RequestedQuantity = line.RequestedQuantity,
                    IsDropped = line.IsDropped,
                    DropReason = line.DropReason
                });
            }
        }

        var validIngredientIds = ingredientForwardPlan.Select(x => x.IngredientId).ToHashSet();
        var orphanIngredientLines = existingDelivery.IngredientItems
            .Where(x => !validIngredientIds.Contains(x.IngredientId))
            .ToList();

        if (orphanIngredientLines.Count > 0)
            _db.DeliveryIngredientItems.RemoveRange(orphanIngredientLines);

        await _db.SaveChangesAsync(ct);
    }

    private sealed class ForwardSnapshotLineMap
    {
        public int ItemId { get; set; }
        public decimal RequestedQuantity { get; set; }
        public decimal ForwardedQuantity { get; set; }
        public decimal DroppedQuantity { get; set; }
        public bool IsDropped { get; set; }
        public string? DropReason { get; set; }
    }

    private async Task<Dictionary<int, ForwardSnapshotLine>> LoadProductForwardSnapshotMapAsync(int orderId, CancellationToken ct)
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
                    ItemId = g.Key,
                    RequestedQuantity = requested,
                    ForwardedQuantity = forwarded,
                    DroppedQuantity = Math.Max(requested - forwarded, 0m),
                    IsDropped = g.Any(x => x.IsDropped),
                    DropReason = reasons.Count == 0 ? null : string.Join(" | ", reasons)
                };
            })
            .ToDictionary(x => x.ItemId, x => x);
    }

    private async Task<Dictionary<int, ForwardSnapshotLine>> LoadIngredientForwardSnapshotMapAsync(int orderId, CancellationToken ct)
    {
        var lines = await _db.DeliveryIngredientItems
            .AsNoTracking()
            .Include(x => x.Delivery)
                .ThenInclude(x => x.DeliveryPlan)
            .Where(x => x.Delivery.DeliveryPlan.StoreOrderId == orderId)
            .ToListAsync(ct);

        return lines
            .GroupBy(x => x.IngredientId)
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
                    ItemId = g.Key,
                    RequestedQuantity = requested,
                    ForwardedQuantity = forwarded,
                    DroppedQuantity = Math.Max(requested - forwarded, 0m),
                    IsDropped = g.Any(x => x.IsDropped),
                    DropReason = reasons.Count == 0 ? null : string.Join(" | ", reasons)
                };
            })
            .ToDictionary(x => x.ItemId, x => x);
    }

    private Dictionary<int, ResolvedForwardSnapshotLine> ResolveForwardSnapshotByProduct(
        StoreOrder order,
        Dictionary<int, ForwardSnapshotLine>? snapshotMap)
    {
        snapshotMap ??= new Dictionary<int, ForwardSnapshotLine>();

        return order.Items
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    snapshotMap.TryGetValue(g.Key, out var snapshot);

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
        Dictionary<int, ForwardSnapshotLine>? snapshotMap)
    {
        snapshotMap ??= new Dictionary<int, ForwardSnapshotLine>();

        return order.IngredientItems
            .GroupBy(x => x.IngredientId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    snapshotMap.TryGetValue(g.Key, out var snapshot);

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
            .Where(x => x.IsUsableNonExpired(today))
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

    private async Task<Dictionary<int, List<InventoryBatchQuantityResponse>>> LoadCentralKitchenIngredientBatchMapAsync(
        int centralKitchenId,
        List<int> ingredientIds,
        CancellationToken ct)
    {
        if (ingredientIds.Count == 0)
            return new();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var batches = (await _db.IngredientBatches
                .AsNoTracking()
                .Include(x => x.Ingredient)
                .Where(x =>
                    x.CentralKitchenId == centralKitchenId &&
                    x.FranchiseId == null &&
                    ingredientIds.Contains(x.IngredientId) &&
                    x.Quantity > 0)
                .ToListAsync(ct))
            .Where(x => x.IsUsableNonExpired(today))
            .ToList();

        return batches
            .GroupBy(x => x.IngredientId)
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
