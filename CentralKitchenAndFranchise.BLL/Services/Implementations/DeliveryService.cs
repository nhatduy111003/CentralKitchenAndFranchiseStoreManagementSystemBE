using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Deliveries;
using CentralKitchenAndFranchise.DTO.Responses.Deliveries;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class DeliveryService : IDeliveryService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFranchiseAccessService _franchiseAccess;

    public DeliveryService(AppDbContext db, ICurrentUserService current, IFranchiseAccessService franchiseAccess)
    {
        _db = db;
        _current = current;
        _franchiseAccess = franchiseAccess;
    }

    public async Task<int> CreatePlanAsync(CreateDeliveryPlanRequest request, CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.SupplyCoordinator);

        if (request.ToFranchiseId <= 0) throw new ArgumentException("ToFranchiseId is required.");

        var exists = await _db.Franchises.AnyAsync(x => x.FranchiseId == request.ToFranchiseId, ct);
        if (!exists) throw new KeyNotFoundException($"Franchise {request.ToFranchiseId} not found.");

        var plan = new DeliveryPlan
        {
            FranchiseId = request.ToFranchiseId,
            PlannedDate = request.PlannedDate
        };

        _db.DeliveryPlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        await AddAuditAsync("DELIVERY_PLAN_CREATE", "DeliveryPlan", plan.DeliveryPlanId, request.ToFranchiseId, null, plan, null, ct);
        return plan.DeliveryPlanId;
    }

    public async Task<int> CreateDeliveryAsync(CreateDeliveryRequest request, CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.SupplyCoordinator);

        if (request.DeliveryPlanId <= 0) throw new ArgumentException("DeliveryPlanId is required.");
        if (request.FromFranchiseId <= 0) throw new ArgumentException("FromFranchiseId is required.");

        var plan = await _db.DeliveryPlans.FirstOrDefaultAsync(p => p.DeliveryPlanId == request.DeliveryPlanId, ct);
        if (plan is null) throw new KeyNotFoundException($"DeliveryPlan {request.DeliveryPlanId} not found.");

        var fromExists = await _db.Franchises.AnyAsync(x => x.FranchiseId == request.FromFranchiseId, ct);
        if (!fromExists) throw new KeyNotFoundException($"From franchise {request.FromFranchiseId} not found.");

        var now = DateTime.UtcNow;

        var delivery = new Delivery
        {
            DeliveryPlanId = request.DeliveryPlanId,
            FromFranchiseId = request.FromFranchiseId,
            Status = DeliveryStatus.Created,
            CreatedAt = now,
            DeliveredAt = now
        };

        _db.Deliveries.Add(delivery);
        await _db.SaveChangesAsync(ct);

        await AddAuditAsync("DELIVERY_CREATE", "Delivery", delivery.DeliveryId, request.FromFranchiseId, null, delivery, null, ct);
        return delivery.DeliveryId;
    }

    public async Task<DeliveryDetailsResponse> GetByIdAsync(int deliveryId, CancellationToken ct = default)
    {
        _ = _current.UserId;

        var delivery = await _db.Deliveries
            .Include(d => d.DeliveryPlan).ThenInclude(p => p.Franchise)
            .Include(d => d.FromFranchise)
            .Include(d => d.ProductItems).ThenInclude(i => i.Product)
            .Include(d => d.IngredientItems).ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, ct);

        if (delivery is null) throw new KeyNotFoundException($"Delivery {deliveryId} not found.");

        return new DeliveryDetailsResponse
        {
            DeliveryId = delivery.DeliveryId,
            DeliveryPlanId = delivery.DeliveryPlanId,
            FromFranchiseId = delivery.FromFranchiseId,
            FromFranchiseName = delivery.FromFranchise.Name,
            ToFranchiseId = delivery.DeliveryPlan.FranchiseId,
            ToFranchiseName = delivery.DeliveryPlan.Franchise.Name,
            Status = delivery.Status,
            PlannedDate = delivery.DeliveryPlan.PlannedDate,
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(delivery.CreatedAt, DateTimeKind.Utc)),
            ConfirmedAt = delivery.ConfirmedAt is null ? null : new DateTimeOffset(DateTime.SpecifyKind(delivery.ConfirmedAt.Value, DateTimeKind.Utc)),
            ProductItems = delivery.ProductItems.Select(x => new DeliveryProductItemDto
            {
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                Quantity = x.Quantity
            }).ToList(),
            IngredientItems = delivery.IngredientItems.Select(x => new DeliveryIngredientItemDto
            {
                IngredientId = x.IngredientId,
                IngredientName = x.Ingredient.Name,
                Quantity = x.Quantity
            }).ToList()
        };
    }

    public async Task UpsertProductItemsAsync(int deliveryId, List<UpsertDeliveryProductItemRequest> items, CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.SupplyCoordinator);
        if (items is null || items.Count == 0) throw new ArgumentException("Items is required.");

        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, ct);
        if (delivery is null) throw new KeyNotFoundException($"Delivery {deliveryId} not found.");
        if (delivery.Status != DeliveryStatus.Created) throw new InvalidOperationException("Can only edit items when delivery is CREATED.");

        var productIds = items.Select(x => x.ProductId).Distinct().ToList();
        var existingProducts = await _db.Products
            .Where(p => productIds.Contains(p.ProductId))
            .Select(p => p.ProductId)
            .ToListAsync(ct);

        var missing = productIds.Except(existingProducts).ToList();
        if (missing.Count > 0) throw new KeyNotFoundException($"Product not found: {string.Join(',', missing)}");

        foreach (var req in items)
        {
            if (req.Quantity <= 0) throw new ArgumentException("Quantity must be > 0.");

            var line = await _db.DeliveryProductItems
                .FirstOrDefaultAsync(x => x.DeliveryId == deliveryId && x.ProductId == req.ProductId, ct);

            if (line is null)
            {
                _db.DeliveryProductItems.Add(new DeliveryProductItem
                {
                    DeliveryId = deliveryId,
                    ProductId = req.ProductId,
                    Quantity = req.Quantity
                });
            }
            else
            {
                line.Quantity = req.Quantity;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertIngredientItemsAsync(int deliveryId, List<UpsertDeliveryIngredientItemRequest> items, CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.SupplyCoordinator);
        if (items is null || items.Count == 0) throw new ArgumentException("Items is required.");

        var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, ct);
        if (delivery is null) throw new KeyNotFoundException($"Delivery {deliveryId} not found.");
        if (delivery.Status != DeliveryStatus.Created) throw new InvalidOperationException("Can only edit items when delivery is CREATED.");

        var ids = items.Select(x => x.IngredientId).Distinct().ToList();
        var existing = await _db.Ingredients
            .Where(p => ids.Contains(p.IngredientId))
            .Select(p => p.IngredientId)
            .ToListAsync(ct);

        var missing = ids.Except(existing).ToList();
        if (missing.Count > 0) throw new KeyNotFoundException($"Ingredient not found: {string.Join(',', missing)}");

        foreach (var req in items)
        {
            if (req.Quantity <= 0) throw new ArgumentException("Quantity must be > 0.");

            var line = await _db.DeliveryIngredientItems
                .FirstOrDefaultAsync(x => x.DeliveryId == deliveryId && x.IngredientId == req.IngredientId, ct);

            if (line is null)
            {
                _db.DeliveryIngredientItems.Add(new DeliveryIngredientItem
                {
                    DeliveryId = deliveryId,
                    IngredientId = req.IngredientId,
                    Quantity = req.Quantity
                });
            }
            else
            {
                line.Quantity = req.Quantity;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task ConfirmAsync(int deliveryId, CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager);

        var delivery = await _db.Deliveries
            .Include(d => d.DeliveryPlan)
            .Include(d => d.ProductItems)
            .Include(d => d.IngredientItems)
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, ct);

        if (delivery is null) throw new KeyNotFoundException($"Delivery {deliveryId} not found.");
        if (delivery.Status != DeliveryStatus.Created) throw new InvalidOperationException("Delivery is not in CREATED status.");

        var fromFranchiseId = delivery.FromFranchiseId;
        var toFranchiseId = delivery.DeliveryPlan.FranchiseId;

        // Manager scope: BOTH from + to
        await _franchiseAccess.EnsureCanAccessAsync(fromFranchiseId, ct);
        await _franchiseAccess.EnsureCanAccessAsync(toFranchiseId, ct);

        if (delivery.ProductItems.Count == 0 && delivery.IngredientItems.Count == 0)
            throw new ArgumentException("Delivery has no items.");

        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (var line in delivery.IngredientItems)
            await TransferIngredientAsync(deliveryId, fromFranchiseId, toFranchiseId, line.IngredientId, line.Quantity, now, ct);

        foreach (var line in delivery.ProductItems)
            await TransferProductAsync(deliveryId, fromFranchiseId, toFranchiseId, line.ProductId, line.Quantity, now, ct);

        delivery.Status = DeliveryStatus.Confirmed;
        delivery.ConfirmedAt = now;
        delivery.DeliveredAt = now;

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync("DELIVERY_CONFIRM", "Delivery", delivery.DeliveryId, fromFranchiseId,
            oldObj: new { Status = DeliveryStatus.Created },
            newObj: new { delivery.Status, delivery.ConfirmedAt },
            reason: null, ct: ct);

        await tx.CommitAsync(ct);
    }

    // ============ FEFO helpers ============
    private async Task TransferIngredientAsync(int deliveryId, int fromFranchiseId, int toFranchiseId, int ingredientId, decimal requiredQty, DateTime now, CancellationToken ct)
    {
        if (requiredQty <= 0) throw new ArgumentException("Quantity must be > 0.");

        var ingredient = await _db.Ingredients.FirstOrDefaultAsync(x => x.IngredientId == ingredientId, ct);
        if (ingredient is null) throw new KeyNotFoundException($"Ingredient {ingredientId} not found.");
        if (!string.Equals(ingredient.Status, IngredientStatus.Active, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Ingredient {ingredientId} is not ACTIVE.");

        var batches = await _db.IngredientBatches
            .Where(b => b.FranchiseId == fromFranchiseId && b.IngredientId == ingredientId && b.Quantity > 0)
            .OrderBy(b => b.ExpiredAt == null)
            .ThenBy(b => b.ExpiredAt)
            .ThenBy(b => b.BatchId)
            .ToListAsync(ct);

        var total = batches.Sum(b => b.Quantity);
        if (total < requiredQty)
            throw new InvalidOperationException($"Insufficient ingredient stock. IngredientId={ingredientId}, required={requiredQty}, available={total}");

        var remain = requiredQty;

        foreach (var src in batches)
        {
            if (remain <= 0) break;

            var take = Math.Min(src.Quantity, remain);
            if (take <= 0) continue;

            src.Quantity -= take;
            _db.InventoryMovements.Add(new InventoryMovement
            {
                BatchId = src.BatchId,
                Type = MovementType.Out,
                Quantity = take,
                CreatedByUserId = _current.UserId,
                Reason = "Delivery confirm (OUT)",
                DeliveryId = deliveryId,
                CreatedAt = now
            });

            var dest = await _db.IngredientBatches.FirstOrDefaultAsync(b =>
                b.FranchiseId == toFranchiseId &&
                b.IngredientId == ingredientId &&
                b.BatchCode == src.BatchCode, ct);

            if (dest is null)
            {
                dest = new IngredientBatch
                {
                    FranchiseId = toFranchiseId,
                    IngredientId = ingredientId,
                    BatchCode = src.BatchCode,
                    ExpiredAt = src.ExpiredAt,
                    Quantity = 0
                };
                _db.IngredientBatches.Add(dest);
            }

            dest.Quantity += take;

            _db.InventoryMovements.Add(new InventoryMovement
            {
                Batch = dest,
                Type = MovementType.In,
                Quantity = take,
                CreatedByUserId = _current.UserId,
                Reason = "Delivery confirm (IN)",
                DeliveryId = deliveryId,
                CreatedAt = now
            });

            remain -= take;
        }
    }

    private async Task TransferProductAsync(int deliveryId, int fromFranchiseId, int toFranchiseId, int productId, decimal requiredQty, DateTime now, CancellationToken ct)
    {
        if (requiredQty <= 0) throw new ArgumentException("Quantity must be > 0.");

        var product = await _db.Products.FirstOrDefaultAsync(x => x.ProductId == productId, ct);
        if (product is null) throw new KeyNotFoundException($"Product {productId} not found.");
        if (!string.Equals(product.Status, IngredientStatus.Active, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Product {productId} is not ACTIVE.");

        var batches = await _db.ProductBatches
            .Where(b => b.FranchiseId == fromFranchiseId && b.ProductId == productId && b.Quantity > 0)
            .OrderBy(b => b.ExpiredAt == null)
            .ThenBy(b => b.ExpiredAt)
            .ThenBy(b => b.BatchId)
            .ToListAsync(ct);

        var total = batches.Sum(b => b.Quantity);
        if (total < requiredQty)
            throw new InvalidOperationException($"Insufficient product stock. ProductId={productId}, required={requiredQty}, available={total}");

        var remain = requiredQty;

        foreach (var src in batches)
        {
            if (remain <= 0) break;

            var take = Math.Min(src.Quantity, remain);
            if (take <= 0) continue;

            src.Quantity -= take;
            _db.ProductMovements.Add(new ProductMovement
            {
                BatchId = src.BatchId,
                Type = MovementType.Out,
                Quantity = take,
                CreatedByUserId = _current.UserId,
                Reason = "Delivery confirm (OUT)",
                DeliveryId = deliveryId,
                CreatedAt = now
            });

            var dest = await _db.ProductBatches.FirstOrDefaultAsync(b =>
                b.FranchiseId == toFranchiseId &&
                b.ProductId == productId &&
                b.BatchCode == src.BatchCode, ct);

            if (dest is null)
            {
                dest = new ProductBatch
                {
                    FranchiseId = toFranchiseId,
                    ProductId = productId,
                    BatchCode = src.BatchCode,
                    ExpiredAt = src.ExpiredAt,
                    Quantity = 0,
                    CreatedAt = now
                };
                _db.ProductBatches.Add(dest);
            }

            dest.Quantity += take;

            _db.ProductMovements.Add(new ProductMovement
            {
                Batch = dest,
                Type = MovementType.In,
                Quantity = take,
                CreatedByUserId = _current.UserId,
                Reason = "Delivery confirm (IN)",
                DeliveryId = deliveryId,
                CreatedAt = now
            });

            remain -= take;
        }
    }

    private void RequireOneOf(params string[] roles)
    {
        var role = _current.Role;
        if (roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase)))
            return;

        throw new UnauthorizedAccessException("You do not have permission for this action.");
    }

    private async Task AddAuditAsync(string action, string entityName, int entityId, int? franchiseId, object? oldObj, object? newObj, string? reason, CancellationToken ct)
    {
        _db.AuditLogs.Add(new AuditLog
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
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task ReceiveConfirmAsync(int deliveryId, CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff);

        var delivery = await _db.Deliveries
            .Include(d => d.DeliveryPlan)
            .Include(d => d.ReceivingReports)
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, ct);

        if (delivery is null)
            throw new KeyNotFoundException($"Delivery {deliveryId} not found.");

        var toFranchiseId = delivery.DeliveryPlan.FranchiseId;

        await _franchiseAccess.EnsureCanAccessAsync(toFranchiseId, ct);

        if (delivery.Status != DeliveryStatus.Confirmed)
            throw new InvalidOperationException("Only CONFIRMED deliveries can be received.");

        var alreadyReceived = delivery.ReceivingReports.Any();
        if (alreadyReceived)
            throw new InvalidOperationException("This delivery has already been received.");

        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var report = new ReceivingReport
        {
            DeliveryId = delivery.DeliveryId,
            ReceivedAt = now
        };

        _db.ReceivingReports.Add(report);

        delivery.Status = "DELIVERED";
        delivery.DeliveredAt = now;

        var storeOrder = await _db.StoreOrders
            .FirstOrDefaultAsync(x =>
                x.FranchiseId == toFranchiseId &&
                x.OrderDate == delivery.DeliveryPlan.PlannedDate &&
                x.Status == StoreOrderStatus.Locked,
                ct);

        if (storeOrder != null)
        {
            storeOrder.Status = "COMPLETED";
            storeOrder.UpdatedAt = now;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = _current.UserId,
            FranchiseId = toFranchiseId,
            Action = "DELIVERY_RECEIVE_CONFIRM",
            EntityName = "Delivery",
            EntityId = delivery.DeliveryId,
            OldDataJson = JsonSerializer.Serialize(new
            {
                Status = DeliveryStatus.Confirmed
            }),
            NewDataJson = JsonSerializer.Serialize(new
            {
                delivery.Status,
                delivery.DeliveredAt,
                ReceivingReportCreated = true
            }),
            Reason = "Store confirmed receiving delivery",
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
