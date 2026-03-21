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
        var remaining = item.Quantity;

        var sourceBatches = await _db.ProductBatches
            .Include(x => x.Product)
            .Where(x =>
                x.CentralKitchenId == centralKitchenId &&
                x.FranchiseId == null &&
                x.ProductId == item.ProductId &&
                x.Quantity > 0)
            .ToListAsync(ct);

        sourceBatches = sourceBatches
            .OrderBy(x => x.CalculateExpiredAt() == null)
            .ThenBy(x => x.CalculateExpiredAt())
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.BatchId)
            .ToList();

        var total = sourceBatches.Sum(x => x.Quantity);
        if (total < remaining)
            throw new InvalidOperationException(
                $"Insufficient central kitchen product stock for productId={item.ProductId}");

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
        var remaining = item.Quantity;

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
            .OrderBy(x => x.CalculateExpiredAt() == null)
            .ThenBy(x => x.CalculateExpiredAt())
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.BatchId)
            .ToList();

        var total = sourceBatches.Sum(x => x.Quantity);
        if (total < remaining)
            throw new InvalidOperationException(
                $"Insufficient central kitchen ingredient stock for ingredientId={item.IngredientId}");

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