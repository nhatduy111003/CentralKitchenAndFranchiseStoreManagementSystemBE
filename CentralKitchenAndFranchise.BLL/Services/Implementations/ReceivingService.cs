using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Extensions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Receivings;
using CentralKitchenAndFranchise.DTO.Responses.Inventory;
using CentralKitchenAndFranchise.DTO.Responses.Receivings;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class ReceivingService : IReceivingService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFranchiseAccessService _access;
    private readonly IInventoryTransferService _transferService;

    public ReceivingService(
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

    public async Task<List<ReceivingListItemResponse>> GetPendingAsync(
    int franchiseId,
    CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff);
        await _access.EnsureCanAccessAsync(franchiseId, ct);

        var deliveries = await _db.Deliveries
            .AsNoTracking()
            .Include(d => d.DeliveryPlan)
                .ThenInclude(p => p.StoreOrder)
            .Include(d => d.FromCentralKitchen)
            .Include(d => d.ReceivingReports)
            .Include(d => d.ProductItems)
            .Include(d => d.IngredientItems)
            .Where(d =>
                d.DeliveryPlan.FranchiseId == franchiseId &&
                d.Status == DeliveryStatus.Delivered &&
                !d.ReceivingReports.Any())
            .OrderByDescending(d => d.DeliveredAt ?? d.CreatedAt)
            .ThenByDescending(d => d.DeliveryId)
            .ToListAsync(ct);

        return deliveries.Select(d =>
        {
            var receivingStatus = ResolveReceivingStatus(d, d.DeliveryPlan.StoreOrder?.Status);
            var canConfirm = CanConfirmReceiving(d, d.DeliveryPlan.StoreOrder?.Status);

            return new ReceivingListItemResponse
            {
                ReceivingId = d.DeliveryId,
                DeliveryCode = BuildDeliveryCode(d.DeliveryId),

                FranchiseId = d.DeliveryPlan.FranchiseId,
                CentralKitchenId = d.FromCentralKitchenId,
                CentralKitchenName = d.FromCentralKitchen?.Name ?? "(unknown)",

                PlanDate = d.DeliveryPlan.PlannedDate,
                DeliveryDate = d.DeliveredAt ?? d.CreatedAt,
                CreatedAt = d.CreatedAt,

                Status = receivingStatus,
                CanConfirm = canConfirm,

                TotalItems =
                    d.ProductItems.Count(x => x.Quantity > 0) +
                    d.IngredientItems.Count(x => x.Quantity > 0),

                TotalQuantity =
                    d.ProductItems.Sum(x => x.Quantity) +
                    d.IngredientItems.Sum(x => x.Quantity),

                StoreOrderId = d.DeliveryPlan.StoreOrderId,
                OrderCode = d.DeliveryPlan.StoreOrderId.HasValue
                    ? BuildOrderCode(d.DeliveryPlan.StoreOrderId.Value)
                    : null
            };
        }).ToList();
    }

    public async Task<ReceivingDetailResponse> GetByIdAsync(
    int franchiseId,
    int deliveryId,
    CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff);
        await _access.EnsureCanAccessAsync(franchiseId, ct);

        var delivery = await _db.Deliveries
            .AsNoTracking()
            .Include(d => d.DeliveryPlan)
                .ThenInclude(p => p.Franchise)
            .Include(d => d.DeliveryPlan)
                .ThenInclude(p => p.StoreOrder)
            .Include(d => d.FromCentralKitchen)
            .Include(d => d.ProductItems)
                .ThenInclude(x => x.Product)
            .Include(d => d.IngredientItems)
                .ThenInclude(x => x.Ingredient)
            .Include(d => d.ReceivingReports)
            .FirstOrDefaultAsync(d =>
                d.DeliveryId == deliveryId &&
                d.DeliveryPlan.FranchiseId == franchiseId,
                ct);

        if (delivery is null)
            throw new KeyNotFoundException($"Receiving/Delivery {deliveryId} not found.");

        var receivingStatus = ResolveReceivingStatus(delivery, delivery.DeliveryPlan.StoreOrder?.Status);
        var canConfirm = CanConfirmReceiving(delivery, delivery.DeliveryPlan.StoreOrder?.Status);

        var latestReport = delivery.ReceivingReports
            .OrderByDescending(x => x.ReceivedAt)
            .FirstOrDefault();

        var isConfirmed = string.Equals(receivingStatus, StoreOrderStatus.ReceivedByStore, StringComparison.OrdinalIgnoreCase);

        var productIds = delivery.ProductItems
            .Select(x => x.ProductId)
            .Distinct()
            .ToList();

        var ingredientIds = delivery.IngredientItems
            .Select(x => x.IngredientId)
            .Distinct()
            .ToList();

        var availableProductBatchMap = await LoadAvailableCentralKitchenProductBatchMapAsync(
            delivery.FromCentralKitchenId,
            productIds,
            ct);

        var availableIngredientBatchMap = await LoadAvailableCentralKitchenIngredientBatchMapAsync(
            delivery.FromCentralKitchenId,
            ingredientIds,
            ct);

        var creditedProductBatchMap = await LoadCreditedProductBatchMapAsync(
            delivery.DeliveryId,
            franchiseId,
            productIds,
            ct);

        var creditedIngredientBatchMap = await LoadCreditedIngredientBatchMapAsync(
            delivery.DeliveryId,
            franchiseId,
            ingredientIds,
            ct);

        var availableProductQtyMap = availableProductBatchMap
            .ToDictionary(x => x.Key, x => x.Value.Sum(b => b.Quantity));

        var availableIngredientQtyMap = availableIngredientBatchMap
            .ToDictionary(x => x.Key, x => x.Value.Sum(b => b.Quantity));

        var creditedProductQtyMap = creditedProductBatchMap
            .ToDictionary(x => x.Key, x => x.Value.Sum(b => b.Quantity));

        var creditedIngredientQtyMap = creditedIngredientBatchMap
            .ToDictionary(x => x.Key, x => x.Value.Sum(b => b.Quantity));

        var response = new ReceivingDetailResponse
        {
            ReceivingId = delivery.DeliveryId,
            DeliveryCode = BuildDeliveryCode(delivery.DeliveryId),
            Status = receivingStatus,
            CanConfirm = canConfirm,

            CentralKitchenId = delivery.FromCentralKitchenId,
            CentralKitchenName = delivery.FromCentralKitchen?.Name ?? "(unknown)",

            FranchiseId = delivery.DeliveryPlan.FranchiseId,
            FranchiseName = delivery.DeliveryPlan.Franchise.Name,

            PlanDate = delivery.DeliveryPlan.PlannedDate,
            DeliveryDate = delivery.DeliveredAt ?? delivery.CreatedAt,
            CreatedAt = delivery.CreatedAt,

            Note = latestReport?.Note,

            StoreOrderId = delivery.DeliveryPlan.StoreOrderId,
            OrderCode = delivery.DeliveryPlan.StoreOrderId.HasValue
                ? BuildOrderCode(delivery.DeliveryPlan.StoreOrderId.Value)
                : null,

            Items = new List<ReceivingDetailLineResponse>()
        };

        response.Items.AddRange(delivery.ProductItems.Select(x =>
        {
            var requestedQuantity = x.RequestedQuantity > 0 ? x.RequestedQuantity : x.Quantity;
            var deliveredQuantity = x.Quantity;
            var creditedQuantity = GetTotalQuantity(creditedProductQtyMap, x.ProductId);

            return new ReceivingDetailLineResponse
            {
                ItemType = "PRODUCT",
                ItemId = x.ProductId,
                ItemName = x.Product?.Name ?? "(unknown)",
                Unit = x.Product?.Unit ?? "",
                ExpectedQuantity = requestedQuantity,
                DeliveredQuantity = deliveredQuantity,
                ReceivedQuantity = isConfirmed ? creditedQuantity : null,

                AvailableInCentralKitchenQuantity = GetTotalQuantity(availableProductQtyMap, x.ProductId),
                AvailableCentralKitchenBatches = GetBatchList(availableProductBatchMap, x.ProductId),

                CreditedToFranchiseQuantity = creditedQuantity,
                CreditedToFranchiseBatches = GetBatchList(creditedProductBatchMap, x.ProductId),

                DroppedQuantity = Math.Max(requestedQuantity - deliveredQuantity, 0m),
                IsDropped = x.IsDropped,
                DropReason = x.DropReason
            };
        }));

        response.Items.AddRange(delivery.IngredientItems.Select(x =>
        {
            var creditedQuantity = GetTotalQuantity(creditedIngredientQtyMap, x.IngredientId);
            var requestedQuantity = x.RequestedQuantity > 0 ? x.RequestedQuantity : x.Quantity;

            return new ReceivingDetailLineResponse
            {
                ItemType = "INGREDIENT",
                ItemId = x.IngredientId,
                ItemName = x.Ingredient?.Name ?? "(unknown)",
                Unit = x.Ingredient?.Unit ?? "",
                ExpectedQuantity = requestedQuantity,
                DeliveredQuantity = x.Quantity,
                ReceivedQuantity = isConfirmed ? creditedQuantity : null,

                AvailableInCentralKitchenQuantity = GetTotalQuantity(availableIngredientQtyMap, x.IngredientId),
                AvailableCentralKitchenBatches = GetBatchList(availableIngredientBatchMap, x.IngredientId),

                CreditedToFranchiseQuantity = creditedQuantity,
                CreditedToFranchiseBatches = GetBatchList(creditedIngredientBatchMap, x.IngredientId),

                DroppedQuantity = Math.Max(requestedQuantity - x.Quantity, 0m),
                IsDropped = x.IsDropped,
                DropReason = x.DropReason
            };
        }));

        return response;
    }

    public async Task<ReceivingConfirmResponse> ConfirmAsync(
        int franchiseId,
        int deliveryId,
        ConfirmReceivingRequest request,
        CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff);
        await _access.EnsureCanAccessAsync(franchiseId, ct);

        var delivery = await _db.Deliveries
            .Include(d => d.DeliveryPlan)
                .ThenInclude(p => p.StoreOrder)
            .Include(d => d.ReceivingReports)
            .FirstOrDefaultAsync(d =>
                d.DeliveryId == deliveryId &&
                d.DeliveryPlan.FranchiseId == franchiseId,
                ct);

        if (delivery is null)
            throw new KeyNotFoundException($"Receiving/Delivery {deliveryId} not found.");

        var receivingStatus = ResolveReceivingStatus(delivery, delivery.DeliveryPlan.StoreOrder?.Status);

        if (!CanConfirmReceiving(delivery, delivery.DeliveryPlan.StoreOrder?.Status))
        {
            throw new InvalidOperationException(
                $"Only {StoreOrderStatus.Delivered} receivings can be confirmed by store. Current status: {receivingStatus}.");
        }

        if (delivery.ReceivingReports.Any())
            throw new InvalidOperationException("This receiving has already been confirmed.");

        if (!delivery.IsStockCommitted)
            throw new InvalidOperationException("Receiving cannot be confirmed because delivery stock has not been committed at prepare.");

        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        await _transferService.FinalizeDeliveryReceivingAsync(
            delivery.DeliveryId,
            franchiseId,
            now,
            ct);

        var report = new ReceivingReport
        {
            DeliveryId = delivery.DeliveryId,
            ReceivedAt = now,
            ReceivedByUserId = _current.UserId,
            Note = string.IsNullOrWhiteSpace(request?.Note) ? null : request.Note.Trim()
        };

        _db.ReceivingReports.Add(report);

        delivery.Status = DeliveryStatus.Confirmed;
        delivery.ConfirmedAt = now;

        if (delivery.DeliveryPlan.StoreOrderId.HasValue)
        {
            var order = await _db.StoreOrders
                .FirstOrDefaultAsync(x => x.StoreOrderId == delivery.DeliveryPlan.StoreOrderId.Value, ct);

            if (order is not null)
            {
                var oldStatus = order.Status;
                order.Status = StoreOrderStatus.ReceivedByStore;

                _db.Set<StoreOrderHistory>().Add(new StoreOrderHistory
                {
                    StoreOrderId = order.StoreOrderId,
                    ActionType = StoreOrderHistoryActions.OrderReceivedByStore,
                    ActionLabel = "Store đã xác nhận nhận hàng",
                    OldStatus = oldStatus,
                    NewStatus = order.Status,
                    Note = report.Note,
                    PerformedByUserId = _current.UserId,
                    PerformedAt = now
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = _current.UserId,
            FranchiseId = franchiseId,
            CentralKitchenId = delivery.FromCentralKitchenId,
            Action = "RECEIVING_CONFIRM",
            EntityName = "Delivery",
            EntityId = delivery.DeliveryId,
            OldDataJson = JsonSerializer.Serialize(new
            {
                DeliveryStatus = DeliveryStatus.Delivered
            }),
            NewDataJson = JsonSerializer.Serialize(new
            {
                DeliveryStatus = delivery.Status,
                delivery.ConfirmedAt,
                ReceivingReportId = report.ReceivingReportId,
                report.ReceivedAt,
                report.ReceivedByUserId,
                report.Note
            }),
            Reason = report.Note,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new ReceivingConfirmResponse
        {
            ReceivingId = delivery.DeliveryId,
            DeliveryCode = BuildDeliveryCode(delivery.DeliveryId),
            Status = StoreOrderStatus.ReceivedByStore,
            ConfirmedAt = report.ReceivedAt,
            InventoryUpdated = true
        };
    }

    private static string ResolveReceivingStatus(Delivery delivery, string? storeOrderStatus = null)
    {
        // Check staus base on ReceivingReport data
        if (delivery.ReceivingReports.Any())
            return StoreOrderStatus.ReceivedByStore;

        if (string.Equals(delivery.Status, DeliveryStatus.Confirmed, StringComparison.OrdinalIgnoreCase))
            return StoreOrderStatus.ReceivedByStore;

        // mapping by lifecycle order priority
        if (!string.IsNullOrWhiteSpace(storeOrderStatus))
        {
            if (string.Equals(storeOrderStatus, StoreOrderStatus.ReceivedByStore, StringComparison.OrdinalIgnoreCase))
                return StoreOrderStatus.ReceivedByStore;

            if (string.Equals(storeOrderStatus, StoreOrderStatus.Delivered, StringComparison.OrdinalIgnoreCase))
                return StoreOrderStatus.Delivered;

            if (string.Equals(storeOrderStatus, StoreOrderStatus.InTransit, StringComparison.OrdinalIgnoreCase))
                return StoreOrderStatus.InTransit;

            if (string.Equals(storeOrderStatus, StoreOrderStatus.ReadyToDeliver, StringComparison.OrdinalIgnoreCase))
                return StoreOrderStatus.ReadyToDeliver;

            if (string.Equals(storeOrderStatus, StoreOrderStatus.Preparing, StringComparison.OrdinalIgnoreCase))
                return StoreOrderStatus.Preparing;
        }

        // Fallback base on delivery if don't have linked order or order status haven't sync
        if (string.Equals(delivery.Status, DeliveryStatus.Delivered, StringComparison.OrdinalIgnoreCase))
            return StoreOrderStatus.Delivered;

        if (string.Equals(delivery.Status, DeliveryStatus.Shipped, StringComparison.OrdinalIgnoreCase))
            return StoreOrderStatus.InTransit;

        if (string.Equals(delivery.Status, DeliveryStatus.Created, StringComparison.OrdinalIgnoreCase))
            return DeliveryStatus.Created;

        if (string.Equals(delivery.Status, DeliveryStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
            return StoreOrderStatus.Cancelled;

        return delivery.Status;
    }

    private static bool CanConfirmReceiving(Delivery delivery, string? storeOrderStatus = null)
    {
        if (delivery.ReceivingReports.Any())
            return false;

        var lifecycleStatus = ResolveReceivingStatus(delivery, storeOrderStatus);

        return string.Equals(lifecycleStatus, StoreOrderStatus.Delivered, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<int, List<InventoryBatchQuantityResponse>>> LoadAvailableCentralKitchenProductBatchMapAsync(
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
                    !x.IsInTransit &&
                    x.DeliveryId == null &&
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

    private async Task<Dictionary<int, List<InventoryBatchQuantityResponse>>> LoadAvailableCentralKitchenIngredientBatchMapAsync(
        int centralKitchenId,
        List<int> ingredientIds,
        CancellationToken ct)
    {
        if (ingredientIds.Count == 0)
            return new();

        var batches = await _db.IngredientBatches
            .AsNoTracking()
            .Include(x => x.Ingredient)
            .Where(x =>
                x.Type == InventoryOwnerType.CentralKitchen &&
                x.CentralKitchenId == centralKitchenId &&
                x.FranchiseId == null &&
                !x.IsInTransit &&
                x.DeliveryId == null &&
                ingredientIds.Contains(x.IngredientId) &&
                x.Quantity > 0)
            .ToListAsync(ct);

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

    private async Task<Dictionary<int, List<InventoryBatchQuantityResponse>>> LoadCreditedProductBatchMapAsync(
        int deliveryId,
        int franchiseId,
        List<int> productIds,
        CancellationToken ct)
    {
        if (productIds.Count == 0)
            return new();

        var movements = await _db.ProductMovements
            .AsNoTracking()
            .Include(x => x.Batch)
                .ThenInclude(x => x.Product)
            .Where(x =>
                x.DeliveryId == deliveryId &&
                x.Type == MovementType.In &&
                x.Batch.FranchiseId == franchiseId &&
                x.Batch.CentralKitchenId == null &&
                !x.Batch.IsInTransit &&
                x.Batch.DeliveryId == null &&
                productIds.Contains(x.Batch.ProductId))
            .ToListAsync(ct);

        return movements
            .GroupBy(x => x.Batch.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .GroupBy(x => x.BatchId)
                    .Select(x =>
                    {
                        var batch = x.First().Batch;

                        return new InventoryBatchQuantityResponse
                        {
                            BatchId = batch.BatchId,
                            BatchCode = batch.BatchCode,
                            Quantity = x.Sum(m => m.Quantity),
                            CreatedAt = batch.CreatedAt,
                            ExpiredAt = batch.CalculateExpiredAt()
                        };
                    })
                    .OrderBy(x => x.ExpiredAt == null)
                    .ThenBy(x => x.ExpiredAt)
                    .ThenBy(x => x.CreatedAt)
                    .ThenBy(x => x.BatchId)
                    .ToList());
    }

    private async Task<Dictionary<int, List<InventoryBatchQuantityResponse>>> LoadCreditedIngredientBatchMapAsync(
        int deliveryId,
        int franchiseId,
        List<int> ingredientIds,
        CancellationToken ct)
    {
        if (ingredientIds.Count == 0)
            return new();

        var movements = await _db.InventoryMovements
            .AsNoTracking()
            .Include(x => x.Batch)
                .ThenInclude(x => x.Ingredient)
            .Where(x =>
                x.DeliveryId == deliveryId &&
                x.Type == InventoryMovementType.In &&
                x.Batch.Type == InventoryOwnerType.Franchise &&
                x.Batch.FranchiseId == franchiseId &&
                x.Batch.CentralKitchenId == null &&
                !x.Batch.IsInTransit &&
                x.Batch.DeliveryId == null &&
                ingredientIds.Contains(x.Batch.IngredientId))
            .ToListAsync(ct);

        return movements
            .GroupBy(x => x.Batch.IngredientId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .GroupBy(x => x.BatchId)
                    .Select(x =>
                    {
                        var batch = x.First().Batch;

                        return new InventoryBatchQuantityResponse
                        {
                            BatchId = batch.BatchId,
                            BatchCode = batch.BatchCode,
                            Quantity = x.Sum(m => m.Quantity),
                            CreatedAt = batch.CreatedAt,
                            ExpiredAt = batch.CalculateExpiredAt()
                        };
                    })
                    .OrderBy(x => x.ExpiredAt == null)
                    .ThenBy(x => x.ExpiredAt)
                    .ThenBy(x => x.CreatedAt)
                    .ThenBy(x => x.BatchId)
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

    private void RequireOneOf(params string[] roles)
    {
        var currentRole = _current.Role;

        if (roles.Any(r => string.Equals(r, currentRole, StringComparison.OrdinalIgnoreCase)))
            return;

        throw new ForbiddenAccessException("You do not have permission for this action.");
    }

    private static string BuildDeliveryCode(int deliveryId)
        => $"DLV-{deliveryId:D6}";

    private static string BuildOrderCode(int storeOrderId)
        => $"SO-{storeOrderId:D6}";
}