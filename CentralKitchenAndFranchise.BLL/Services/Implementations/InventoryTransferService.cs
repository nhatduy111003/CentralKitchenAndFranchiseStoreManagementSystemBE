using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Extensions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.BLL.Services.Models.InventoryHistory;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class InventoryTransferService : IInventoryTransferService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IInventoryLedgerWriter _inventoryLedgerWriter;

    public InventoryTransferService(
        AppDbContext db,
        ICurrentUserService current,
        IInventoryLedgerWriter inventoryLedgerWriter)
    {
        _db = db;
        _current = current;
        _inventoryLedgerWriter = inventoryLedgerWriter;
    }

    public async Task CommitDeliveryStockAsync(
        Delivery delivery,
        int toFranchiseId,
        DateTime now,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        var deliveryFlow = await LoadDeliveryFlowInfoAsync(delivery.DeliveryId, ct);
        if (deliveryFlow.ToFranchiseId != toFranchiseId)
            throw new InvalidOperationException($"Delivery {delivery.DeliveryId} does not belong to franchise {toFranchiseId}.");

        await EnsurePrepareLedgerNotAlreadyWrittenAsync(delivery.DeliveryId, ct);

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

        var pendingPrepareLedgerPairs = new List<PendingPrepareLedgerPair>();

        foreach (var line in productLines)
        {
            sourceProductMap.TryGetValue(line.ProductId, out var sourceBatches);
            sourceBatches ??= [];

            var lineLedgerPairs = new List<PendingPrepareLedgerPair>();
            var actualCommittedQty = CommitProductLine(
                line,
                sourceBatches,
                transitProductMap,
                deliveryId,
                toFranchiseId,
                now,
                lineLedgerPairs);

            ReconcileCommittedQuantity(line, actualCommittedQty, "product");
            ApplyLineSnapshots(lineLedgerPairs, line.RequestedQuantity, line.Quantity, line.DropReason);
            pendingPrepareLedgerPairs.AddRange(lineLedgerPairs);
        }

        foreach (var line in ingredientLines)
        {
            sourceIngredientMap.TryGetValue(line.IngredientId, out var sourceBatches);
            sourceBatches ??= [];

            var lineLedgerPairs = new List<PendingPrepareLedgerPair>();
            var actualCommittedQty = CommitIngredientLine(
                line,
                sourceBatches,
                transitIngredientMap,
                deliveryId,
                toFranchiseId,
                now,
                lineLedgerPairs);

            ReconcileCommittedQuantity(line, actualCommittedQty, "ingredient");
            ApplyLineSnapshots(lineLedgerPairs, line.RequestedQuantity, line.Quantity, line.DropReason);
            pendingPrepareLedgerPairs.AddRange(lineLedgerPairs);
        }

        var committedTotal = delivery.ProductItems.Sum(x => x.Quantity) + delivery.IngredientItems.Sum(x => x.Quantity);
        if (committedTotal <= 0m)
        {
            throw new InvalidOperationException(
                "Cannot prepare delivery because there is no usable stock left to ship for this order. All delivery lines have been adjusted to 0 during commit.");
        }

        if (pendingPrepareLedgerPairs.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            await AppendPrepareLedgerAsync(deliveryFlow, now, pendingPrepareLedgerPairs, ct);
        }
    }

    public async Task FinalizeDeliveryReceivingAsync(
        int deliveryId,
        int toFranchiseId,
        DateTime now,
        CancellationToken ct = default)
    {
        var deliveryFlow = await LoadDeliveryFlowInfoAsync(deliveryId, ct);
        if (deliveryFlow.ToFranchiseId != toFranchiseId)
            throw new InvalidOperationException($"Delivery {deliveryId} does not belong to franchise {toFranchiseId}.");

        await EnsureReceivingLedgerNotAlreadyWrittenAsync(deliveryId, ct);

        var productLineSnapshots = await _db.DeliveryProductItems
            .AsNoTracking()
            .Where(x => x.DeliveryId == deliveryId)
            .ToDictionaryAsync(
                x => x.ProductId,
                x => new DeliveryLineLedgerSnapshot
                {
                    RequestedQuantity = x.RequestedQuantity > 0m ? x.RequestedQuantity : x.Quantity,
                    ActualQuantity = x.Quantity,
                    DroppedQuantity = Math.Max((x.RequestedQuantity > 0m ? x.RequestedQuantity : x.Quantity) - x.Quantity, 0m),
                    DropReason = x.DropReason,
                    MetadataJson = BuildDeliveryLineMetadataJson(InventoryHistoryItemTypes.Product, x.DeliveryProductItemId)
                },
                ct);

        var ingredientLineSnapshots = await _db.DeliveryIngredientItems
            .AsNoTracking()
            .Where(x => x.DeliveryId == deliveryId)
            .ToDictionaryAsync(
                x => x.IngredientId,
                x => new DeliveryLineLedgerSnapshot
                {
                    RequestedQuantity = x.RequestedQuantity > 0m ? x.RequestedQuantity : x.Quantity,
                    ActualQuantity = x.Quantity,
                    DroppedQuantity = Math.Max((x.RequestedQuantity > 0m ? x.RequestedQuantity : x.Quantity) - x.Quantity, 0m),
                    DropReason = x.DropReason,
                    MetadataJson = BuildDeliveryLineMetadataJson(InventoryHistoryItemTypes.Ingredient, x.DeliveryIngredientItemId)
                },
                ct);

        var transitProductBatches = await _db.ProductBatches
            .Include(x => x.Product)
            .Where(x =>
                x.FranchiseId == toFranchiseId &&
                x.CentralKitchenId == null &&
                x.IsInTransit &&
                x.DeliveryId == deliveryId &&
                x.Quantity > 0)
            .ToListAsync(ct);

        var transitIngredientBatches = await _db.IngredientBatches
            .Include(x => x.Ingredient)
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

        var pendingReceiveLedgerPairs = new List<PendingReceiveLedgerPair>();

        foreach (var transit in transitProductBatches
                     .OrderBy(x => x.ProductId)
                     .ThenBy(x => x.CreatedAt)
                     .ThenBy(x => x.BatchId))
        {
            productLineSnapshots.TryGetValue(transit.ProductId, out var lineSnapshot);
            lineSnapshot ??= DeliveryLineLedgerSnapshot.Empty;

            FinalizeProductTransitBatch(
                transit,
                onHandProductMap,
                deliveryId,
                toFranchiseId,
                now,
                lineSnapshot,
                pendingReceiveLedgerPairs);
        }

        foreach (var transit in transitIngredientBatches
                     .OrderBy(x => x.IngredientId)
                     .ThenBy(x => x.CreatedAt)
                     .ThenBy(x => x.BatchId))
        {
            ingredientLineSnapshots.TryGetValue(transit.IngredientId, out var lineSnapshot);
            lineSnapshot ??= DeliveryLineLedgerSnapshot.Empty;

            FinalizeIngredientTransitBatch(
                transit,
                onHandIngredientMap,
                deliveryId,
                toFranchiseId,
                now,
                lineSnapshot,
                pendingReceiveLedgerPairs);
        }

        if (pendingReceiveLedgerPairs.Count > 0)
            await AppendReceivingLedgerAsync(deliveryFlow, now, pendingReceiveLedgerPairs, ct);
    }

    private void FinalizeProductTransitBatch(
        ProductBatch transitBatch,
        Dictionary<(int ProductId, string BatchCode), ProductBatch> onHandBatchMap,
        int deliveryId,
        int franchiseId,
        DateTime now,
        DeliveryLineLedgerSnapshot lineSnapshot,
        List<PendingReceiveLedgerPair> pendingLedgerPairs)
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
            pendingLedgerPairs.Add(BuildPendingReceiveLedgerPair(
                itemType: InventoryHistoryItemTypes.Product,
                itemId: transitBatch.ProductId,
                quantity: qty,
                transitBatchId: transitBatch.BatchId,
                receiveBatchId: transitBatch.BatchId,
                batchCodeSnapshot: transitBatch.BatchCode,
                batchCreatedAtUtc: transitBatch.CreatedAt,
                expiredAtSnapshot: transitBatch.CalculateExpiredAt(),
                requestedQuantitySnapshot: lineSnapshot.RequestedQuantity,
                actualQuantitySnapshot: lineSnapshot.ActualQuantity,
                droppedQuantitySnapshot: lineSnapshot.DroppedQuantity,
                dropReasonSnapshot: lineSnapshot.DropReason,
                metadataJson: lineSnapshot.MetadataJson));
            return;
        }

        if (onHandBatch.CreatedAt != transitBatch.CreatedAt)
        {
            throw new InvalidOperationException(
                $"Product batch age conflict for BatchCode={transitBatch.BatchCode} at destination franchise {franchiseId}.");
        }

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

        pendingLedgerPairs.Add(BuildPendingReceiveLedgerPair(
            itemType: InventoryHistoryItemTypes.Product,
            itemId: transitBatch.ProductId,
            quantity: qty,
            transitBatchId: transitBatch.BatchId,
            receiveBatchId: onHandBatch.BatchId,
            batchCodeSnapshot: transitBatch.BatchCode,
            batchCreatedAtUtc: transitBatch.CreatedAt,
            expiredAtSnapshot: transitBatch.CalculateExpiredAt(),
            requestedQuantitySnapshot: lineSnapshot.RequestedQuantity,
            actualQuantitySnapshot: lineSnapshot.ActualQuantity,
            droppedQuantitySnapshot: lineSnapshot.DroppedQuantity,
            dropReasonSnapshot: lineSnapshot.DropReason,
            metadataJson: lineSnapshot.MetadataJson));
    }

    private void FinalizeIngredientTransitBatch(
        IngredientBatch transitBatch,
        Dictionary<(int IngredientId, string BatchCode), IngredientBatch> onHandBatchMap,
        int deliveryId,
        int franchiseId,
        DateTime now,
        DeliveryLineLedgerSnapshot lineSnapshot,
        List<PendingReceiveLedgerPair> pendingLedgerPairs)
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
            pendingLedgerPairs.Add(BuildPendingReceiveLedgerPair(
                itemType: InventoryHistoryItemTypes.Ingredient,
                itemId: transitBatch.IngredientId,
                quantity: qty,
                transitBatchId: transitBatch.BatchId,
                receiveBatchId: transitBatch.BatchId,
                batchCodeSnapshot: transitBatch.BatchCode,
                batchCreatedAtUtc: transitBatch.CreatedAt,
                expiredAtSnapshot: transitBatch.CalculateExpiredAt(),
                requestedQuantitySnapshot: lineSnapshot.RequestedQuantity,
                actualQuantitySnapshot: lineSnapshot.ActualQuantity,
                droppedQuantitySnapshot: lineSnapshot.DroppedQuantity,
                dropReasonSnapshot: lineSnapshot.DropReason,
                metadataJson: lineSnapshot.MetadataJson));
            return;
        }

        if (onHandBatch.CreatedAt != transitBatch.CreatedAt)
        {
            throw new InvalidOperationException(
                $"Ingredient batch age conflict for BatchCode={transitBatch.BatchCode} at destination franchise {franchiseId}.");
        }

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

        pendingLedgerPairs.Add(BuildPendingReceiveLedgerPair(
            itemType: InventoryHistoryItemTypes.Ingredient,
            itemId: transitBatch.IngredientId,
            quantity: qty,
            transitBatchId: transitBatch.BatchId,
            receiveBatchId: onHandBatch.BatchId,
            batchCodeSnapshot: transitBatch.BatchCode,
            batchCreatedAtUtc: transitBatch.CreatedAt,
            expiredAtSnapshot: transitBatch.CalculateExpiredAt(),
            requestedQuantitySnapshot: lineSnapshot.RequestedQuantity,
            actualQuantitySnapshot: lineSnapshot.ActualQuantity,
            droppedQuantitySnapshot: lineSnapshot.DroppedQuantity,
            dropReasonSnapshot: lineSnapshot.DropReason,
            metadataJson: lineSnapshot.MetadataJson));
    }

    private decimal CommitProductLine(
        DeliveryProductItem line,
        List<ProductBatch> sourceBatches,
        Dictionary<(int ProductId, string BatchCode), ProductBatch> transitBatchMap,
        int deliveryId,
        int franchiseId,
        DateTime now,
        List<PendingPrepareLedgerPair> pendingLedgerPairs)
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
                Reason = DeliveryMovementReasons.PrepareOutToTransit,
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
                    Quantity = 0m,
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
                Reason = DeliveryMovementReasons.PrepareInTransit,
                DeliveryId = deliveryId,
                CreatedAt = now
            });

            pendingLedgerPairs.Add(new PendingPrepareLedgerPair
            {
                ItemType = InventoryHistoryItemTypes.Product,
                ItemId = src.ProductId,
                Quantity = take,
                SourceBatchId = src.BatchId,
                SourceBatchCodeSnapshot = src.BatchCode,
                SourceBatchCreatedAtUtc = src.CreatedAt,
                ExpiredAtSnapshot = src.CalculateExpiredAt(),
                TransitProductBatch = transitBatch,
                MetadataJson = BuildDeliveryLineMetadataJson(InventoryHistoryItemTypes.Product, line.DeliveryProductItemId)
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
        DateTime now,
        List<PendingPrepareLedgerPair> pendingLedgerPairs)
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
                Type = InventoryMovementType.Out,
                Quantity = take,
                CreatedByUserId = _current.UserId,
                Reason = DeliveryMovementReasons.PrepareOutToTransit,
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
                    Quantity = 0m,
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
                Type = InventoryMovementType.In,
                Quantity = take,
                CreatedByUserId = _current.UserId,
                Reason = DeliveryMovementReasons.PrepareInTransit,
                DeliveryId = deliveryId,
                CreatedAt = now
            });

            pendingLedgerPairs.Add(new PendingPrepareLedgerPair
            {
                ItemType = InventoryHistoryItemTypes.Ingredient,
                ItemId = src.IngredientId,
                Quantity = take,
                SourceBatchId = src.BatchId,
                SourceBatchCodeSnapshot = src.BatchCode,
                SourceBatchCreatedAtUtc = src.CreatedAt,
                ExpiredAtSnapshot = src.CalculateExpiredAt(),
                TransitIngredientBatch = transitBatch,
                MetadataJson = BuildDeliveryLineMetadataJson(InventoryHistoryItemTypes.Ingredient, line.DeliveryIngredientItemId)
            });
        }

        return committed;
    }

    private async Task AppendPrepareLedgerAsync(
        DeliveryFlowInfo deliveryFlow,
        DateTime occurredAtUtc,
        IReadOnlyList<PendingPrepareLedgerPair> pendingPairs,
        CancellationToken ct)
    {
        var sequenceNo = 1;
        var items = new List<InventoryLedgerWriteItem>(pendingPairs.Count * 2);

        foreach (var pair in pendingPairs)
        {
            var transitBatchId = ResolveTransitBatchId(pair);
            var transitBatchCode = ResolveTransitBatchCode(pair);
            var transitBatchCreatedAtUtc = ResolveTransitBatchCreatedAtUtc(pair);

            items.Add(new InventoryLedgerWriteItem
            {
                SequenceNo = sequenceNo++,
                ItemType = pair.ItemType,
                ItemId = pair.ItemId,
                BatchId = pair.SourceBatchId,
                BatchCodeSnapshot = pair.SourceBatchCodeSnapshot,
                BatchCreatedAtUtc = SpecifyUtc(pair.SourceBatchCreatedAtUtc),
                ExpiredAtSnapshot = pair.ExpiredAtSnapshot,
                ScopeType = InventoryLedgerScopeTypes.CentralKitchen,
                ScopeId = deliveryFlow.FromCentralKitchenId,
                StockBucket = InventoryLedgerStockBuckets.OnHand,
                DeltaQuantity = -pair.Quantity,
                EventType = InventoryLedgerEventTypes.PrepareOut,
                Reason = DeliveryMovementReasons.PrepareOutToTransit,
                ActorUserId = _current.UserId,
                ReferenceType = InventoryLedgerReferenceTypes.Delivery,
                ReferenceId = deliveryFlow.DeliveryId,
                DeliveryId = deliveryFlow.DeliveryId,
                DeliveryPlanId = deliveryFlow.DeliveryPlanId,
                StoreOrderId = deliveryFlow.StoreOrderId,
                RequestedQuantitySnapshot = pair.RequestedQuantitySnapshot,
                ActualQuantitySnapshot = pair.ActualQuantitySnapshot,
                DroppedQuantitySnapshot = pair.DroppedQuantitySnapshot,
                DropReasonSnapshot = pair.DropReasonSnapshot,
                CounterpartyScopeType = InventoryLedgerScopeTypes.Franchise,
                CounterpartyScopeId = deliveryFlow.ToFranchiseId,
                CounterpartyBatchId = transitBatchId,
                MetadataJson = pair.MetadataJson
            });

            items.Add(new InventoryLedgerWriteItem
            {
                SequenceNo = sequenceNo++,
                ItemType = pair.ItemType,
                ItemId = pair.ItemId,
                BatchId = transitBatchId,
                BatchCodeSnapshot = transitBatchCode,
                BatchCreatedAtUtc = SpecifyUtc(transitBatchCreatedAtUtc),
                ExpiredAtSnapshot = pair.ExpiredAtSnapshot,
                ScopeType = InventoryLedgerScopeTypes.Franchise,
                ScopeId = deliveryFlow.ToFranchiseId,
                StockBucket = InventoryLedgerStockBuckets.Transit,
                DeltaQuantity = pair.Quantity,
                EventType = InventoryLedgerEventTypes.TransitIn,
                Reason = DeliveryMovementReasons.PrepareInTransit,
                ActorUserId = _current.UserId,
                ReferenceType = InventoryLedgerReferenceTypes.Delivery,
                ReferenceId = deliveryFlow.DeliveryId,
                DeliveryId = deliveryFlow.DeliveryId,
                DeliveryPlanId = deliveryFlow.DeliveryPlanId,
                StoreOrderId = deliveryFlow.StoreOrderId,
                RequestedQuantitySnapshot = pair.RequestedQuantitySnapshot,
                ActualQuantitySnapshot = pair.ActualQuantitySnapshot,
                DroppedQuantitySnapshot = pair.DroppedQuantitySnapshot,
                DropReasonSnapshot = pair.DropReasonSnapshot,
                CounterpartyScopeType = InventoryLedgerScopeTypes.CentralKitchen,
                CounterpartyScopeId = deliveryFlow.FromCentralKitchenId,
                CounterpartyBatchId = pair.SourceBatchId,
                MetadataJson = pair.MetadataJson
            });
        }

        await _inventoryLedgerWriter.AppendAsync(new InventoryLedgerWriteRequest
        {
            CorrelationId = Guid.NewGuid(),
            OccurredAtUtc = SpecifyUtc(occurredAtUtc),
            SaveChanges = false,
            Items = items
        }, ct);
    }

    private async Task AppendReceivingLedgerAsync(
        DeliveryFlowInfo deliveryFlow,
        DateTime occurredAtUtc,
        IReadOnlyList<PendingReceiveLedgerPair> pendingPairs,
        CancellationToken ct)
    {
        var sequenceNo = 1;
        var items = new List<InventoryLedgerWriteItem>(pendingPairs.Count * 2);

        foreach (var pair in pendingPairs)
        {
            items.Add(new InventoryLedgerWriteItem
            {
                SequenceNo = sequenceNo++,
                ItemType = pair.ItemType,
                ItemId = pair.ItemId,
                BatchId = pair.TransitBatchId,
                BatchCodeSnapshot = pair.BatchCodeSnapshot,
                BatchCreatedAtUtc = SpecifyUtc(pair.BatchCreatedAtUtc),
                ExpiredAtSnapshot = pair.ExpiredAtSnapshot,
                ScopeType = InventoryLedgerScopeTypes.Franchise,
                ScopeId = deliveryFlow.ToFranchiseId,
                StockBucket = InventoryLedgerStockBuckets.Transit,
                DeltaQuantity = -pair.Quantity,
                EventType = InventoryLedgerEventTypes.TransitOut,
                Reason = DeliveryMovementReasons.ReceivingOutTransit,
                ActorUserId = _current.UserId,
                ReferenceType = InventoryLedgerReferenceTypes.Delivery,
                ReferenceId = deliveryFlow.DeliveryId,
                DeliveryId = deliveryFlow.DeliveryId,
                DeliveryPlanId = deliveryFlow.DeliveryPlanId,
                StoreOrderId = deliveryFlow.StoreOrderId,
                RequestedQuantitySnapshot = pair.RequestedQuantitySnapshot,
                ActualQuantitySnapshot = pair.ActualQuantitySnapshot,
                DroppedQuantitySnapshot = pair.DroppedQuantitySnapshot,
                DropReasonSnapshot = pair.DropReasonSnapshot,
                CounterpartyScopeType = InventoryLedgerScopeTypes.Franchise,
                CounterpartyScopeId = deliveryFlow.ToFranchiseId,
                CounterpartyBatchId = pair.ReceiveBatchId,
                MetadataJson = pair.MetadataJson
            });

            items.Add(new InventoryLedgerWriteItem
            {
                SequenceNo = sequenceNo++,
                ItemType = pair.ItemType,
                ItemId = pair.ItemId,
                BatchId = pair.ReceiveBatchId,
                BatchCodeSnapshot = pair.BatchCodeSnapshot,
                BatchCreatedAtUtc = SpecifyUtc(pair.BatchCreatedAtUtc),
                ExpiredAtSnapshot = pair.ExpiredAtSnapshot,
                ScopeType = InventoryLedgerScopeTypes.Franchise,
                ScopeId = deliveryFlow.ToFranchiseId,
                StockBucket = InventoryLedgerStockBuckets.OnHand,
                DeltaQuantity = pair.Quantity,
                EventType = InventoryLedgerEventTypes.ReceiveIn,
                Reason = DeliveryMovementReasons.ReceivingInOnHand,
                ActorUserId = _current.UserId,
                ReferenceType = InventoryLedgerReferenceTypes.Delivery,
                ReferenceId = deliveryFlow.DeliveryId,
                DeliveryId = deliveryFlow.DeliveryId,
                DeliveryPlanId = deliveryFlow.DeliveryPlanId,
                StoreOrderId = deliveryFlow.StoreOrderId,
                RequestedQuantitySnapshot = pair.RequestedQuantitySnapshot,
                ActualQuantitySnapshot = pair.ActualQuantitySnapshot,
                DroppedQuantitySnapshot = pair.DroppedQuantitySnapshot,
                DropReasonSnapshot = pair.DropReasonSnapshot,
                CounterpartyScopeType = InventoryLedgerScopeTypes.Franchise,
                CounterpartyScopeId = deliveryFlow.ToFranchiseId,
                CounterpartyBatchId = pair.TransitBatchId,
                MetadataJson = pair.MetadataJson
            });
        }

        await _inventoryLedgerWriter.AppendAsync(new InventoryLedgerWriteRequest
        {
            CorrelationId = Guid.NewGuid(),
            OccurredAtUtc = SpecifyUtc(occurredAtUtc),
            SaveChanges = false,
            Items = items
        }, ct);
    }

    private async Task<DeliveryFlowInfo> LoadDeliveryFlowInfoAsync(int deliveryId, CancellationToken ct)
    {
        var flow = await _db.Deliveries
            .AsNoTracking()
            .Where(x => x.DeliveryId == deliveryId)
            .Select(x => new DeliveryFlowInfo
            {
                DeliveryId = x.DeliveryId,
                DeliveryPlanId = x.DeliveryPlanId,
                StoreOrderId = x.DeliveryPlan.StoreOrderId,
                FromCentralKitchenId = x.FromCentralKitchenId,
                ToFranchiseId = x.DeliveryPlan.FranchiseId
            })
            .FirstOrDefaultAsync(ct);

        if (flow is null)
            throw new KeyNotFoundException($"Delivery {deliveryId} was not found.");

        return flow;
    }

    private async Task EnsurePrepareLedgerNotAlreadyWrittenAsync(int deliveryId, CancellationToken ct)
    {
        var alreadyPrepared = await _db.InventoryLedgerEntries
            .AsNoTracking()
            .AnyAsync(x =>
                x.DeliveryId == deliveryId &&
                (x.EventType == InventoryLedgerEventTypes.PrepareOut ||
                 x.EventType == InventoryLedgerEventTypes.TransitIn),
                ct);

        if (alreadyPrepared)
            throw new InvalidOperationException("Delivery prepare ledger rows already exist for this delivery.");
    }

    private async Task EnsureReceivingLedgerNotAlreadyWrittenAsync(int deliveryId, CancellationToken ct)
    {
        var alreadyReceived = await _db.InventoryLedgerEntries
            .AsNoTracking()
            .AnyAsync(x =>
                x.DeliveryId == deliveryId &&
                (x.EventType == InventoryLedgerEventTypes.TransitOut ||
                 x.EventType == InventoryLedgerEventTypes.ReceiveIn),
                ct);

        if (alreadyReceived)
            throw new InvalidOperationException("Receiving ledger rows already exist for this delivery.");
    }

    private static void ApplyLineSnapshots(
        IEnumerable<PendingPrepareLedgerPair> lineLedgerPairs,
        decimal requestedQuantity,
        decimal actualQuantity,
        string? dropReason)
    {
        var droppedQuantity = Math.Max(requestedQuantity - actualQuantity, 0m);

        foreach (var pair in lineLedgerPairs)
        {
            pair.RequestedQuantitySnapshot = requestedQuantity;
            pair.ActualQuantitySnapshot = actualQuantity;
            pair.DroppedQuantitySnapshot = droppedQuantity;
            pair.DropReasonSnapshot = dropReason;
        }
    }

    private static PendingReceiveLedgerPair BuildPendingReceiveLedgerPair(
        string itemType,
        int itemId,
        decimal quantity,
        int transitBatchId,
        int receiveBatchId,
        string batchCodeSnapshot,
        DateTime batchCreatedAtUtc,
        DateOnly? expiredAtSnapshot,
        decimal requestedQuantitySnapshot,
        decimal actualQuantitySnapshot,
        decimal droppedQuantitySnapshot,
        string? dropReasonSnapshot,
        string? metadataJson)
    {
        return new PendingReceiveLedgerPair
        {
            ItemType = itemType,
            ItemId = itemId,
            Quantity = quantity,
            TransitBatchId = transitBatchId,
            ReceiveBatchId = receiveBatchId,
            BatchCodeSnapshot = batchCodeSnapshot,
            BatchCreatedAtUtc = batchCreatedAtUtc,
            ExpiredAtSnapshot = expiredAtSnapshot,
            RequestedQuantitySnapshot = requestedQuantitySnapshot,
            ActualQuantitySnapshot = actualQuantitySnapshot,
            DroppedQuantitySnapshot = droppedQuantitySnapshot,
            DropReasonSnapshot = dropReasonSnapshot,
            MetadataJson = metadataJson
        };
    }

    private static int ResolveTransitBatchId(PendingPrepareLedgerPair pair)
        => pair.TransitProductBatch?.BatchId
           ?? pair.TransitIngredientBatch?.BatchId
           ?? throw new InvalidOperationException("Prepare ledger pair is missing transit batch reference.");

    private static string ResolveTransitBatchCode(PendingPrepareLedgerPair pair)
        => pair.TransitProductBatch?.BatchCode
           ?? pair.TransitIngredientBatch?.BatchCode
           ?? throw new InvalidOperationException("Prepare ledger pair is missing transit batch reference.");

    private static DateTime ResolveTransitBatchCreatedAtUtc(PendingPrepareLedgerPair pair)
        => pair.TransitProductBatch?.CreatedAt
           ?? pair.TransitIngredientBatch?.CreatedAt
           ?? throw new InvalidOperationException("Prepare ledger pair is missing transit batch reference.");

    private static DateTime SpecifyUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string BuildDeliveryLineMetadataJson(string itemType, int deliveryItemId)
        => JsonSerializer.Serialize(new
        {
            ItemType = itemType,
            DeliveryItemId = deliveryItemId
        });

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

    private sealed class DeliveryFlowInfo
    {
        public int DeliveryId { get; set; }
        public int DeliveryPlanId { get; set; }
        public int? StoreOrderId { get; set; }
        public int FromCentralKitchenId { get; set; }
        public int ToFranchiseId { get; set; }
    }

    private sealed class DeliveryLineLedgerSnapshot
    {
        public static DeliveryLineLedgerSnapshot Empty { get; } = new();

        public decimal RequestedQuantity { get; set; }
        public decimal ActualQuantity { get; set; }
        public decimal DroppedQuantity { get; set; }
        public string? DropReason { get; set; }
        public string? MetadataJson { get; set; }
    }

    private sealed class PendingPrepareLedgerPair
    {
        public string ItemType { get; set; } = default!;
        public int ItemId { get; set; }
        public decimal Quantity { get; set; }
        public int SourceBatchId { get; set; }
        public string SourceBatchCodeSnapshot { get; set; } = default!;
        public DateTime SourceBatchCreatedAtUtc { get; set; }
        public DateOnly? ExpiredAtSnapshot { get; set; }
        public ProductBatch? TransitProductBatch { get; set; }
        public IngredientBatch? TransitIngredientBatch { get; set; }
        public decimal RequestedQuantitySnapshot { get; set; }
        public decimal ActualQuantitySnapshot { get; set; }
        public decimal DroppedQuantitySnapshot { get; set; }
        public string? DropReasonSnapshot { get; set; }
        public string? MetadataJson { get; set; }
    }

    private sealed class PendingReceiveLedgerPair
    {
        public string ItemType { get; set; } = default!;
        public int ItemId { get; set; }
        public decimal Quantity { get; set; }
        public int TransitBatchId { get; set; }
        public int ReceiveBatchId { get; set; }
        public string BatchCodeSnapshot { get; set; } = default!;
        public DateTime BatchCreatedAtUtc { get; set; }
        public DateOnly? ExpiredAtSnapshot { get; set; }
        public decimal RequestedQuantitySnapshot { get; set; }
        public decimal ActualQuantitySnapshot { get; set; }
        public decimal DroppedQuantitySnapshot { get; set; }
        public string? DropReasonSnapshot { get; set; }
        public string? MetadataJson { get; set; }
    }
}
