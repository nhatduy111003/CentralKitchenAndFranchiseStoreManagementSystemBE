using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Extensions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class InventoryTransferService : IInventoryTransferService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public InventoryTransferService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task CommitDeliveryStockAsync(
        Delivery delivery,
        int toFranchiseId,
        DateTime now,
        CancellationToken ct = default)
    {
        if (delivery is null)
            throw new ArgumentNullException(nameof(delivery));

        var today = DateOnly.FromDateTime(now);
        var deliveryId = delivery.DeliveryId;
        var fromCentralKitchenId = delivery.FromCentralKitchenId;

        var productLines = delivery.ProductItems
            .Where(x => x.RequestedQuantity > 0)
            .OrderBy(x => x.DeliveryProductItemId)
            .ToList();

        var ingredientLines = delivery.IngredientItems
            .Where(x => x.RequestedQuantity > 0)
            .OrderBy(x => x.DeliveryIngredientItemId)
            .ToList();

        var productIds = productLines.Select(x => x.ProductId).Distinct().ToList();
        var ingredientIds = ingredientLines.Select(x => x.IngredientId).Distinct().ToList();

        var sourceProductMap = productIds.Count == 0
            ? new Dictionary<int, List<ProductBatch>>()
            : (await _db.ProductBatches
                .Include(x => x.Product)
                .Where(x =>
                    x.CentralKitchenId == fromCentralKitchenId &&
                    x.FranchiseId == null &&
                    productIds.Contains(x.ProductId) &&
                    x.Quantity > 0 &&
                    !x.IsInTransit &&
                    x.DeliveryId == null)
                .ToListAsync(ct))
                .Where(x => x.IsUsableNonExpired(today))
                .GroupBy(x => x.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(x => x.CalculateExpiredAt() == null)
                          .ThenBy(x => x.CalculateExpiredAt())
                          .ThenBy(x => x.CreatedAt)
                          .ThenBy(x => x.BatchId)
                          .ToList());

        var sourceIngredientMap = ingredientIds.Count == 0
            ? new Dictionary<int, List<IngredientBatch>>()
            : (await _db.IngredientBatches
                .Include(x => x.Ingredient)
                .Where(x =>
                    x.Type == InventoryOwnerType.CentralKitchen &&
                    x.CentralKitchenId == fromCentralKitchenId &&
                    x.FranchiseId == null &&
                    ingredientIds.Contains(x.IngredientId) &&
                    x.Quantity > 0 &&
                    !x.IsInTransit &&
                    x.DeliveryId == null)
                .ToListAsync(ct))
                .Where(x => x.IsUsableNonExpired(today))
                .GroupBy(x => x.IngredientId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(x => x.CalculateExpiredAt() == null)
                          .ThenBy(x => x.CalculateExpiredAt())
                          .ThenBy(x => x.CreatedAt)
                          .ThenBy(x => x.BatchId)
                          .ToList());

        var transitProductMap = productIds.Count == 0
            ? new Dictionary<(int ProductId, string BatchCode), ProductBatch>()
            : (await _db.ProductBatches
                .Where(x =>
                    x.FranchiseId == toFranchiseId &&
                    x.CentralKitchenId == null &&
                    x.IsInTransit &&
                    x.DeliveryId == deliveryId &&
                    productIds.Contains(x.ProductId))
                .ToListAsync(ct))
                .ToDictionary(x => (x.ProductId, x.BatchCode), x => x);

        var transitIngredientMap = ingredientIds.Count == 0
            ? new Dictionary<(int IngredientId, string BatchCode), IngredientBatch>()
            : (await _db.IngredientBatches
                .Where(x =>
                    x.Type == InventoryOwnerType.Franchise &&
                    x.FranchiseId == toFranchiseId &&
                    x.CentralKitchenId == null &&
                    x.IsInTransit &&
                    x.DeliveryId == deliveryId &&
                    ingredientIds.Contains(x.IngredientId))
                .ToListAsync(ct))
                .ToDictionary(x => (x.IngredientId, x.BatchCode), x => x);

        foreach (var line in productLines)
        {
            sourceProductMap.TryGetValue(line.ProductId, out var sourceBatches);
            sourceBatches ??= [];

            var actualCommittedQty = CommitProductLine(
                line,
                sourceBatches,
                transitProductMap,
                deliveryId,
                toFranchiseId,
                now);

            ReconcileCommittedQuantity(line, actualCommittedQty, "product");
        }

        foreach (var line in ingredientLines)
        {
            sourceIngredientMap.TryGetValue(line.IngredientId, out var sourceBatches);
            sourceBatches ??= [];

            var actualCommittedQty = CommitIngredientLine(
                line,
                sourceBatches,
                transitIngredientMap,
                deliveryId,
                toFranchiseId,
                now);

            ReconcileCommittedQuantity(line, actualCommittedQty, "ingredient");
        }

        var committedTotal = delivery.ProductItems.Sum(x => x.Quantity) + delivery.IngredientItems.Sum(x => x.Quantity);
        if (committedTotal <= 0m)
        {
            throw new InvalidOperationException(
                "Cannot prepare delivery because there is no usable stock left to ship for this order. All delivery lines have been adjusted to 0 during commit.");
        }
    }

    public async Task FinalizeDeliveryReceivingAsync(
    int deliveryId,
    int toFranchiseId,
    DateTime now,
    CancellationToken ct = default)
    {
        var transitProductBatches = await _db.ProductBatches
            .Where(x =>
                x.FranchiseId == toFranchiseId &&
                x.CentralKitchenId == null &&
                x.IsInTransit &&
                x.DeliveryId == deliveryId &&
                x.Quantity > 0)
            .ToListAsync(ct);

        var transitIngredientBatches = await _db.IngredientBatches
            .Where(x =>
                x.Type == InventoryOwnerType.Franchise &&
                x.FranchiseId == toFranchiseId &&
                x.CentralKitchenId == null &&
                x.IsInTransit &&
                x.DeliveryId == deliveryId &&
                x.Quantity > 0)
            .ToListAsync(ct);

        var totalTransitQty = transitProductBatches.Sum(x => x.Quantity) + transitIngredientBatches.Sum(x => x.Quantity);
        if (totalTransitQty <= 0m)
        {
            throw new InvalidOperationException(
                "No in-transit stock exists for this delivery. Receiving cannot be finalized.");
        }

        var productIds = transitProductBatches.Select(x => x.ProductId).Distinct().ToList();
        var ingredientIds = transitIngredientBatches.Select(x => x.IngredientId).Distinct().ToList();

        var onHandProductMap = productIds.Count == 0
            ? new Dictionary<(int ProductId, string BatchCode), ProductBatch>()
            : (await _db.ProductBatches
                .Where(x =>
                    x.FranchiseId == toFranchiseId &&
                    x.CentralKitchenId == null &&
                    !x.IsInTransit &&
                    x.DeliveryId == null &&
                    productIds.Contains(x.ProductId))
                .ToListAsync(ct))
                .ToDictionary(x => (x.ProductId, x.BatchCode), x => x);

        var onHandIngredientMap = ingredientIds.Count == 0
            ? new Dictionary<(int IngredientId, string BatchCode), IngredientBatch>()
            : (await _db.IngredientBatches
                .Where(x =>
                    x.Type == InventoryOwnerType.Franchise &&
                    x.FranchiseId == toFranchiseId &&
                    x.CentralKitchenId == null &&
                    !x.IsInTransit &&
                    x.DeliveryId == null &&
                    ingredientIds.Contains(x.IngredientId))
                .ToListAsync(ct))
                .ToDictionary(x => (x.IngredientId, x.BatchCode), x => x);

        foreach (var transit in transitProductBatches
                     .OrderBy(x => x.ProductId)
                     .ThenBy(x => x.CreatedAt)
                     .ThenBy(x => x.BatchId))
        {
            FinalizeProductTransitBatch(transit, onHandProductMap, deliveryId, toFranchiseId, now);
        }

        foreach (var transit in transitIngredientBatches
                     .OrderBy(x => x.IngredientId)
                     .ThenBy(x => x.CreatedAt)
                     .ThenBy(x => x.BatchId))
        {
            FinalizeIngredientTransitBatch(transit, onHandIngredientMap, deliveryId, toFranchiseId, now);
        }
    }

    private void FinalizeProductTransitBatch(
    ProductBatch transitBatch,
    Dictionary<(int ProductId, string BatchCode), ProductBatch> onHandBatchMap,
    int deliveryId,
    int franchiseId,
    DateTime now)
    {
        if (transitBatch.Quantity <= 0m)
            return;

        var qty = transitBatch.Quantity;

        _db.ProductMovements.Add(new ProductMovement
        {
            BatchId = transitBatch.BatchId,
            Type = MovementType.Out,
            Quantity = qty,
            CreatedByUserId = _current.UserId,
            Reason = DeliveryMovementReasons.ReceivingOutTransit,
            DeliveryId = deliveryId,
            CreatedAt = now
        });

        var key = (transitBatch.ProductId, transitBatch.BatchCode);
        if (!onHandBatchMap.TryGetValue(key, out var onHandBatch))
        {
            // No matching on-hand batch exists -> flip the transit row into normal on-hand stock.
            transitBatch.IsInTransit = false;
            transitBatch.DeliveryId = null;

            _db.ProductMovements.Add(new ProductMovement
            {
                BatchId = transitBatch.BatchId,
                Type = MovementType.In,
                Quantity = qty,
                CreatedByUserId = _current.UserId,
                Reason = DeliveryMovementReasons.ReceivingInOnHand,
                DeliveryId = deliveryId,
                CreatedAt = now
            });

            onHandBatchMap[key] = transitBatch;
            return;
        }

        if (onHandBatch.CreatedAt != transitBatch.CreatedAt)
        {
            throw new InvalidOperationException(
                $"Product batch age conflict for BatchCode={transitBatch.BatchCode} at destination franchise {franchiseId}.");
        }

        // Matching on-hand batch exists -> merge quantity into it.
        // We intentionally keep the transit row at quantity 0 instead of deleting it because
        // movement history is stored by BatchId and deleting the row would break traceability.
        transitBatch.Quantity = 0m;
        onHandBatch.Quantity += qty;

        _db.ProductMovements.Add(new ProductMovement
        {
            BatchId = onHandBatch.BatchId,
            Type = MovementType.In,
            Quantity = qty,
            CreatedByUserId = _current.UserId,
            Reason = DeliveryMovementReasons.ReceivingInOnHand,
            DeliveryId = deliveryId,
            CreatedAt = now
        });
    }

    private void FinalizeIngredientTransitBatch(
    IngredientBatch transitBatch,
    Dictionary<(int IngredientId, string BatchCode), IngredientBatch> onHandBatchMap,
    int deliveryId,
    int franchiseId,
    DateTime now)
    {
        if (transitBatch.Quantity <= 0m)
            return;

        var qty = transitBatch.Quantity;

        _db.InventoryMovements.Add(new InventoryMovement
        {
            BatchId = transitBatch.BatchId,
            Type = InventoryMovementType.Out,
            Quantity = qty,
            CreatedByUserId = _current.UserId,
            Reason = DeliveryMovementReasons.ReceivingOutTransit,
            DeliveryId = deliveryId,
            CreatedAt = now
        });

        var key = (transitBatch.IngredientId, transitBatch.BatchCode);
        if (!onHandBatchMap.TryGetValue(key, out var onHandBatch))
        {
            // No matching on-hand batch exists -> flip the transit row into normal on-hand stock.
            transitBatch.IsInTransit = false;
            transitBatch.DeliveryId = null;

            _db.InventoryMovements.Add(new InventoryMovement
            {
                BatchId = transitBatch.BatchId,
                Type = InventoryMovementType.In,
                Quantity = qty,
                CreatedByUserId = _current.UserId,
                Reason = DeliveryMovementReasons.ReceivingInOnHand,
                DeliveryId = deliveryId,
                CreatedAt = now
            });

            onHandBatchMap[key] = transitBatch;
            return;
        }

        if (onHandBatch.CreatedAt != transitBatch.CreatedAt)
        {
            throw new InvalidOperationException(
                $"Ingredient batch age conflict for BatchCode={transitBatch.BatchCode} at destination franchise {franchiseId}.");
        }

        // Matching on-hand batch exists -> merge quantity into it.
        // We intentionally keep the transit row at quantity 0 instead of deleting it because
        // movement history is stored by BatchId and deleting the row would break traceability.
        transitBatch.Quantity = 0m;
        onHandBatch.Quantity += qty;

        _db.InventoryMovements.Add(new InventoryMovement
        {
            BatchId = onHandBatch.BatchId,
            Type = InventoryMovementType.In,
            Quantity = qty,
            CreatedByUserId = _current.UserId,
            Reason = DeliveryMovementReasons.ReceivingInOnHand,
            DeliveryId = deliveryId,
            CreatedAt = now
        });
    }

    // Helpers
    private decimal CommitProductLine(
        DeliveryProductItem line,
        List<ProductBatch> sourceBatches,
        Dictionary<(int ProductId, string BatchCode), ProductBatch> transitBatchMap,
        int deliveryId,
        int franchiseId,
        DateTime now)
    {
        if (line.Quantity <= 0m)
            return 0m;

        var remaining = line.Quantity;
        var committed = 0m;

        foreach (var src in sourceBatches)
        {
            if (remaining <= 0m)
                break;

            var take = Math.Min(src.Quantity, remaining);
            if (take <= 0m)
                continue;

            src.Quantity -= take;
            remaining -= take;
            committed += take;

            _db.ProductMovements.Add(new ProductMovement
            {
                BatchId = src.BatchId,
                Type = MovementType.Out,
                Quantity = take,
                CreatedByUserId = _current.UserId,
                Reason = "Delivery prepare commit (OUT to transit)",
                DeliveryId = deliveryId,
                CreatedAt = now
            });

            var key = (src.ProductId, src.BatchCode);
            if (!transitBatchMap.TryGetValue(key, out var transitBatch))
            {
                transitBatch = new ProductBatch
                {
                    FranchiseId = franchiseId,
                    CentralKitchenId = null,
                    ProductId = src.ProductId,
                    BatchCode = src.BatchCode,
                    Quantity = 0,
                    CreatedAt = src.CreatedAt,
                    IsInTransit = true,
                    DeliveryId = deliveryId
                };

                transitBatchMap[key] = transitBatch;
                _db.ProductBatches.Add(transitBatch);
            }
            else if (transitBatch.CreatedAt != src.CreatedAt)
            {
                throw new InvalidOperationException(
                    $"Transit product batch age conflict for BatchCode={src.BatchCode}, DeliveryId={deliveryId}.");
            }

            transitBatch.Quantity += take;

            _db.ProductMovements.Add(new ProductMovement
            {
                Batch = transitBatch,
                Type = MovementType.In,
                Quantity = take,
                CreatedByUserId = _current.UserId,
                Reason = "Delivery prepare commit (IN transit)",
                DeliveryId = deliveryId,
                CreatedAt = now
            });
        }

        return committed;
    }

    private decimal CommitIngredientLine(
        DeliveryIngredientItem line,
        List<IngredientBatch> sourceBatches,
        Dictionary<(int IngredientId, string BatchCode), IngredientBatch> transitBatchMap,
        int deliveryId,
        int franchiseId,
        DateTime now)
    {
        if (line.Quantity <= 0m)
            return 0m;

        var remaining = line.Quantity;
        var committed = 0m;

        foreach (var src in sourceBatches)
        {
            if (remaining <= 0m)
                break;

            var take = Math.Min(src.Quantity, remaining);
            if (take <= 0m)
                continue;

            src.Quantity -= take;
            remaining -= take;
            committed += take;

            _db.InventoryMovements.Add(new InventoryMovement
            {
                BatchId = src.BatchId,
                Type = MovementType.Out,
                Quantity = take,
                CreatedByUserId = _current.UserId,
                Reason = "Delivery prepare commit (OUT to transit)",
                DeliveryId = deliveryId,
                CreatedAt = now
            });

            var key = (src.IngredientId, src.BatchCode);
            if (!transitBatchMap.TryGetValue(key, out var transitBatch))
            {
                transitBatch = new IngredientBatch
                {
                    Type = InventoryOwnerType.Franchise,
                    FranchiseId = franchiseId,
                    CentralKitchenId = null,
                    IngredientId = src.IngredientId,
                    BatchCode = src.BatchCode,
                    Quantity = 0,
                    CreatedAt = src.CreatedAt,
                    IsInTransit = true,
                    DeliveryId = deliveryId
                };

                transitBatchMap[key] = transitBatch;
                _db.IngredientBatches.Add(transitBatch);
            }
            else if (transitBatch.CreatedAt != src.CreatedAt)
            {
                throw new InvalidOperationException(
                    $"Transit ingredient batch age conflict for BatchCode={src.BatchCode}, DeliveryId={deliveryId}.");
            }

            transitBatch.Quantity += take;

            _db.InventoryMovements.Add(new InventoryMovement
            {
                Batch = transitBatch,
                Type = MovementType.In,
                Quantity = take,
                CreatedByUserId = _current.UserId,
                Reason = "Delivery prepare commit (IN transit)",
                DeliveryId = deliveryId,
                CreatedAt = now
            });
        }

        return committed;
    }

    private static void ReconcileCommittedQuantity(
        DeliveryProductItem line,
        decimal actualCommittedQty,
        string itemType)
    {
        line.Quantity = actualCommittedQty;
        line.IsDropped = actualCommittedQty < line.RequestedQuantity;
        line.DropReason = line.IsDropped
            ? (actualCommittedQty == 0m
                ? $"Fully dropped at commit – no usable {itemType} stock remained. Required={line.RequestedQuantity}."
                : $"Partial drop at commit – stock changed before atomic commit. Required={line.RequestedQuantity}, Shipped={actualCommittedQty}.")
            : null;
    }

    private static void ReconcileCommittedQuantity(
       DeliveryIngredientItem line,
       decimal actualCommittedQty,
       string itemType)
    {
        line.Quantity = actualCommittedQty;
        line.IsDropped = actualCommittedQty < line.RequestedQuantity;
        line.DropReason = line.IsDropped
            ? (actualCommittedQty == 0m
                ? $"Fully dropped at commit – no usable {itemType} stock remained. Required={line.RequestedQuantity}."
                : $"Partial drop at commit – stock changed before atomic commit. Required={line.RequestedQuantity}, Shipped={actualCommittedQty}.")
            : null;
    }

}