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

    public async Task TransferDeliveryAsync(
        int deliveryId,
        int fromCentralKitchenId,
        int toFranchiseId,
        DateTime now,
        CancellationToken ct = default)
    {
        var delivery = await _db.Deliveries
            .Include(x => x.ProductItems)
                .ThenInclude(x => x.Product)
            .Include(x => x.IngredientItems)
                .ThenInclude(x => x.Ingredient)
            .FirstOrDefaultAsync(x => x.DeliveryId == deliveryId, ct);

        if (delivery is null)
            throw new KeyNotFoundException("Delivery not found.");

        foreach (var item in delivery.ProductItems)
        {
            await TransferProductAsync(item, fromCentralKitchenId, toFranchiseId, deliveryId, now, ct);
        }

        foreach (var item in delivery.IngredientItems)
        {
            await TransferIngredientAsync(item, fromCentralKitchenId, toFranchiseId, deliveryId, now, ct);
        }
    }

    private async Task TransferProductAsync(
    DeliveryProductItem item,
    int centralKitchenId,
    int franchiseId,
    int deliveryId,
    DateTime now,
    CancellationToken ct)
    {
        if (item.Quantity <= 0)
            return;

        var remaining = item.Quantity;
        var today = DateOnly.FromDateTime(now);

        var sourceBatches = await _db.ProductBatches
            .Include(x => x.Product)
            .Where(x =>
                x.CentralKitchenId == centralKitchenId &&
                x.FranchiseId == null &&
                x.ProductId == item.ProductId &&
                x.Quantity > 0)
            .ToListAsync(ct);

        sourceBatches = sourceBatches
            .Where(x => x.IsUsableNonExpired(today))
            .OrderBy(x => x.CalculateExpiredAt() == null)
            .ThenBy(x => x.CalculateExpiredAt())
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.BatchId)
            .ToList();

        var total = sourceBatches.Sum(x => x.Quantity);
        if (total < remaining)
            throw new InvalidOperationException(
                $"Insufficient usable central kitchen product stock for productId={item.ProductId}");

        foreach (var src in sourceBatches)
        {
            if (remaining <= 0) break;

            var take = Math.Min(src.Quantity, remaining);
            if (take <= 0) continue;

            src.Quantity -= take;
            remaining -= take;

            _db.ProductMovements.Add(new ProductMovement
            {
                BatchId = src.BatchId,
                Type = MovementType.Out,
                Quantity = take,
                CreatedByUserId = _current.UserId,
                Reason = "Store receiving confirm (OUT)",
                DeliveryId = deliveryId,
                CreatedAt = now
            });

            var dest = await _db.ProductBatches.FirstOrDefaultAsync(x =>
                x.FranchiseId == franchiseId &&
                x.CentralKitchenId == null &&
                x.ProductId == src.ProductId &&
                x.BatchCode == src.BatchCode, ct);

            if (dest is null)
            {
                dest = new ProductBatch
                {
                    FranchiseId = franchiseId,
                    CentralKitchenId = null,
                    ProductId = src.ProductId,
                    BatchCode = src.BatchCode,
                    Quantity = 0,
                    CreatedAt = src.CreatedAt
                };

                _db.ProductBatches.Add(dest);
            }
            else if (dest.CreatedAt != src.CreatedAt)
            {
                throw new InvalidOperationException(
                    $"Product batch age conflict for BatchCode={src.BatchCode} at destination franchise {franchiseId}.");
            }

            dest.Quantity += take;

            _db.ProductMovements.Add(new ProductMovement
            {
                Batch = dest,
                Type = MovementType.In,
                Quantity = take,
                CreatedByUserId = _current.UserId,
                Reason = "Store receiving confirm (IN)",
                DeliveryId = deliveryId,
                CreatedAt = now
            });
        }
    }

    private async Task TransferIngredientAsync(
    DeliveryIngredientItem item,
    int centralKitchenId,
    int franchiseId,
    int deliveryId,
    DateTime now,
    CancellationToken ct)
    {
        if (item.Quantity <= 0)
            return;

        var remaining = item.Quantity;
        var today = DateOnly.FromDateTime(now);

        var sourceBatches = await _db.IngredientBatches
            .Include(x => x.Ingredient)
            .Where(x =>
                x.Type == InventoryOwnerType.CentralKitchen &&
                x.CentralKitchenId == centralKitchenId &&
                x.FranchiseId == null &&
                x.IngredientId == item.IngredientId &&
                x.Quantity > 0)
            .ToListAsync(ct);

        sourceBatches = sourceBatches
            .Where(x => x.IsUsableNonExpired(today))
            .OrderBy(x => x.CalculateExpiredAt() == null)
            .ThenBy(x => x.CalculateExpiredAt())
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.BatchId)
            .ToList();

        var total = sourceBatches.Sum(x => x.Quantity);
        if (total < remaining)
            throw new InvalidOperationException(
                $"Insufficient usable central kitchen ingredient stock for ingredientId={item.IngredientId}");

        foreach (var src in sourceBatches)
        {
            if (remaining <= 0) break;

            var take = Math.Min(src.Quantity, remaining);
            if (take <= 0) continue;

            src.Quantity -= take;
            remaining -= take;

            _db.InventoryMovements.Add(new InventoryMovement
            {
                BatchId = src.BatchId,
                Type = MovementType.Out,
                Quantity = take,
                CreatedByUserId = _current.UserId,
                Reason = "Store receiving confirm (OUT)",
                DeliveryId = deliveryId,
                CreatedAt = now
            });

            var dest = await _db.IngredientBatches.FirstOrDefaultAsync(x =>
                x.Type == InventoryOwnerType.Franchise &&
                x.FranchiseId == franchiseId &&
                x.CentralKitchenId == null &&
                x.IngredientId == src.IngredientId &&
                x.BatchCode == src.BatchCode, ct);

            if (dest is null)
            {
                dest = new IngredientBatch
                {
                    Type = InventoryOwnerType.Franchise,
                    FranchiseId = franchiseId,
                    CentralKitchenId = null,
                    IngredientId = src.IngredientId,
                    BatchCode = src.BatchCode,
                    Quantity = 0,
                    CreatedAt = src.CreatedAt
                };

                _db.IngredientBatches.Add(dest);
            }
            else if (dest.CreatedAt != src.CreatedAt)
            {
                throw new InvalidOperationException(
                    $"Ingredient batch age conflict for BatchCode={src.BatchCode} at destination franchise {franchiseId}.");
            }

            dest.Quantity += take;

            _db.InventoryMovements.Add(new InventoryMovement
            {
                Batch = dest,
                Type = MovementType.In,
                Quantity = take,
                CreatedByUserId = _current.UserId,
                Reason = "Store receiving confirm (IN)",
                DeliveryId = deliveryId,
                CreatedAt = now
            });
        }
    }
}