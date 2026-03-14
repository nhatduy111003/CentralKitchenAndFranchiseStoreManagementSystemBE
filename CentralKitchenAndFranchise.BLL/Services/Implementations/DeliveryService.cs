using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Extensions;
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

    public DeliveryService(
        AppDbContext db,
        ICurrentUserService current,
        IFranchiseAccessService franchiseAccess)
    {
        _db = db;
        _current = current;
        _franchiseAccess = franchiseAccess;
    }

    public async Task<int> CreatePlanAsync(CreateDeliveryPlanRequest request, CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.SupplyCoordinator);

        if (request.ToFranchiseId <= 0)
            throw new ArgumentException("ToFranchiseId is required.");

        var franchise = await _db.Franchises
            .AsNoTracking()
            .Where(x => x.FranchiseId == request.ToFranchiseId)
            .Select(x => new
            {
                x.FranchiseId,
                x.CentralKitchenId
            })
            .FirstOrDefaultAsync(ct);

        if (franchise is null)
            throw new KeyNotFoundException($"Franchise {request.ToFranchiseId} not found.");

        await _franchiseAccess.EnsureCanAccessAsync(franchise.FranchiseId, ct);
        await _franchiseAccess.EnsureCanAccessCentralKitchenAsync(franchise.CentralKitchenId, ct);

        var plan = new DeliveryPlan
        {
            FranchiseId = franchise.FranchiseId,
            CentralKitchenId = franchise.CentralKitchenId,
            PlannedDate = request.PlannedDate
        };

        _db.DeliveryPlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "DELIVERY_PLAN_CREATE",
            entityName: "DeliveryPlan",
            entityId: plan.DeliveryPlanId,
            franchiseId: request.ToFranchiseId,
            oldObj: null,
            newObj: plan,
            reason: null,
            ct: ct);

        return plan.DeliveryPlanId;
    }

    public async Task<int> CreateDeliveryAsync(CreateDeliveryRequest request, CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.SupplyCoordinator);

        if (request.DeliveryPlanId <= 0)
            throw new ArgumentException("DeliveryPlanId is required.");

        if (request.FromCentralKitchenId <= 0)
            throw new ArgumentException("FromCentralKitchenId is required.");

        var plan = await _db.DeliveryPlans
            .FirstOrDefaultAsync(p => p.DeliveryPlanId == request.DeliveryPlanId, ct);

        if (plan is null)
            throw new KeyNotFoundException($"DeliveryPlan {request.DeliveryPlanId} not found.");

        var planCentralKitchenId = await EnsurePlanScopeAsync(plan, ct, persistResolvedCentralKitchenId: true);

        if (request.FromCentralKitchenId != planCentralKitchenId)
            throw new InvalidOperationException(
                $"Delivery source central kitchen must match the plan scope. expected={planCentralKitchenId}, actual={request.FromCentralKitchenId}");

        await _franchiseAccess.EnsureCanAccessCentralKitchenAsync(request.FromCentralKitchenId, ct);
        await _franchiseAccess.EnsureCanAccessAsync(plan.FranchiseId, ct);

        var fromExists = await _db.CentralKitchens
            .AnyAsync(x => x.CentralKitchenId == request.FromCentralKitchenId, ct);

        if (!fromExists)
            throw new KeyNotFoundException($"CentralKitchen {request.FromCentralKitchenId} not found.");

        var now = DateTime.UtcNow;

        var delivery = new Delivery
        {
            DeliveryPlanId = request.DeliveryPlanId,
            FromCentralKitchenId = request.FromCentralKitchenId,
            Status = DeliveryStatus.Created,
            CreatedAt = now,
            DeliveredAt = now
        };

        _db.Deliveries.Add(delivery);
        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "DELIVERY_CREATE",
            entityName: "Delivery",
            entityId: delivery.DeliveryId,
            franchiseId: plan.FranchiseId,
            oldObj: null,
            newObj: delivery,
            reason: null,
            ct: ct);

        return delivery.DeliveryId;
    }

    public async Task<DeliveryDetailsResponse> GetByIdAsync(int deliveryId, CancellationToken ct = default)
    {
        var delivery = await _db.Deliveries
            .Include(d => d.DeliveryPlan)
                .ThenInclude(p => p.Franchise)
            .Include(d => d.FromCentralKitchen)
            .Include(d => d.ProductItems)
                .ThenInclude(i => i.Product)
            .Include(d => d.IngredientItems)
                .ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, ct);

        if (delivery is null)
            throw new KeyNotFoundException($"Delivery {deliveryId} not found.");

        await EnsureDeliveryScopeAsync(delivery, ct);

        return new DeliveryDetailsResponse
        {
            DeliveryId = delivery.DeliveryId,
            DeliveryPlanId = delivery.DeliveryPlanId,
            FromCentralKitchenId = delivery.FromCentralKitchenId,
            FromCentralKitchenName = delivery.FromCentralKitchen.Name,
            ToFranchiseId = delivery.DeliveryPlan.FranchiseId,
            ToFranchiseName = delivery.DeliveryPlan.Franchise.Name,
            Status = delivery.Status,
            PlannedDate = delivery.DeliveryPlan.PlannedDate,
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(delivery.CreatedAt, DateTimeKind.Utc)),
            ConfirmedAt = delivery.ConfirmedAt is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(delivery.ConfirmedAt.Value, DateTimeKind.Utc)),
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

        if (items is null || items.Count == 0)
            throw new ArgumentException("Items is required.");

        var delivery = await _db.Deliveries
            .Include(d => d.DeliveryPlan)
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, ct);

        if (delivery is null)
            throw new KeyNotFoundException($"Delivery {deliveryId} not found.");

        await EnsureDeliveryScopeAsync(delivery, ct);

        if (delivery.Status != DeliveryStatus.Created)
            throw new InvalidOperationException("Can only edit items when delivery is CREATED.");

        var productIds = items.Select(x => x.ProductId).Distinct().ToList();

        var existingProducts = await _db.Products
            .Where(p => productIds.Contains(p.ProductId))
            .Select(p => p.ProductId)
            .ToListAsync(ct);

        var missing = productIds.Except(existingProducts).ToList();
        if (missing.Count > 0)
            throw new KeyNotFoundException($"Product not found: {string.Join(',', missing)}");

        foreach (var req in items)
        {
            if (req.Quantity <= 0)
                throw new ArgumentException("Quantity must be > 0.");

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

        if (items is null || items.Count == 0)
            throw new ArgumentException("Items is required.");

        var delivery = await _db.Deliveries
            .Include(d => d.DeliveryPlan)
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, ct);

        if (delivery is null)
            throw new KeyNotFoundException($"Delivery {deliveryId} not found.");

        await EnsureDeliveryScopeAsync(delivery, ct);

        if (delivery.Status != DeliveryStatus.Created)
            throw new InvalidOperationException("Can only edit items when delivery is CREATED.");

        var ingredientIds = items.Select(x => x.IngredientId).Distinct().ToList();

        var existingIngredients = await _db.Ingredients
            .Where(i => ingredientIds.Contains(i.IngredientId))
            .Select(i => i.IngredientId)
            .ToListAsync(ct);

        var missing = ingredientIds.Except(existingIngredients).ToList();
        if (missing.Count > 0)
            throw new KeyNotFoundException($"Ingredient not found: {string.Join(',', missing)}");

        foreach (var req in items)
        {
            if (req.Quantity <= 0)
                throw new ArgumentException("Quantity must be > 0.");

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
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.SupplyCoordinator);

        var delivery = await _db.Deliveries
            .Include(d => d.DeliveryPlan)
            .Include(d => d.ProductItems)
            .Include(d => d.IngredientItems)
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, ct);

        if (delivery is null)
            throw new KeyNotFoundException($"Delivery {deliveryId} not found.");

        await EnsureDeliveryScopeAsync(delivery, ct);

        if (delivery.Status != DeliveryStatus.Created)
            throw new InvalidOperationException("Delivery is not in CREATED status.");

        var fromCentralKitchenId = delivery.FromCentralKitchenId;
        var toFranchiseId = delivery.DeliveryPlan.FranchiseId;

        if (delivery.ProductItems.Count == 0 && delivery.IngredientItems.Count == 0)
            throw new ArgumentException("Delivery has no items.");

        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (var line in delivery.IngredientItems)
        {
            await TransferIngredientAsync(
                deliveryId,
                fromCentralKitchenId,
                toFranchiseId,
                line.IngredientId,
                line.Quantity,
                now,
                ct);
        }

        foreach (var line in delivery.ProductItems)
        {
            await TransferProductAsync(
                deliveryId,
                fromCentralKitchenId,
                toFranchiseId,
                line.ProductId,
                line.Quantity,
                now,
                ct);
        }

        delivery.Status = DeliveryStatus.Confirmed;
        delivery.ConfirmedAt = now;
        delivery.DeliveredAt = now;

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "DELIVERY_CONFIRM",
            entityName: "Delivery",
            entityId: delivery.DeliveryId,
            franchiseId: toFranchiseId,
            oldObj: new { Status = DeliveryStatus.Created },
            newObj: new { delivery.Status, delivery.ConfirmedAt },
            reason: null,
            ct: ct);

        await tx.CommitAsync(ct);
    }

    // =========================================================
    // FEFO transfer helpers
    // =========================================================

    private async Task TransferIngredientAsync(
        int deliveryId,
        int fromCentralKitchenId,
        int toFranchiseId,
        int ingredientId,
        decimal requiredQty,
        DateTime now,
        CancellationToken ct)
    {
        if (requiredQty <= 0)
            throw new ArgumentException("Quantity must be > 0.");

        var ingredient = await _db.Ingredients
            .FirstOrDefaultAsync(x => x.IngredientId == ingredientId, ct);

        if (ingredient is null)
            throw new KeyNotFoundException($"Ingredient {ingredientId} not found.");

        if (!string.Equals(ingredient.Status, IngredientStatus.Active, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Ingredient {ingredientId} is not ACTIVE.");

        // Vì ExpiredAt là derived => phải Include Ingredient rồi sort in-memory
        var batches = await _db.IngredientBatches
            .Include(b => b.Ingredient)
            .Where(b =>
                b.Type == InventoryOwnerType.CentralKitchen &&
                b.CentralKitchenId == fromCentralKitchenId &&
                b.IngredientId == ingredientId &&
                b.Quantity > 0)
            .ToListAsync(ct);

        batches = batches
            .OrderBy(b => b.CalculateExpiredAt() == null)
            .ThenBy(b => b.CalculateExpiredAt())
            .ThenBy(b => b.CreatedAt)
            .ThenBy(b => b.BatchId)
            .ToList();

        var total = batches.Sum(b => b.Quantity);
        if (total < requiredQty)
            throw new InvalidOperationException(
                $"Insufficient ingredient stock. IngredientId={ingredientId}, required={requiredQty}, available={total}");

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
                b.Type == InventoryOwnerType.Franchise &&
                b.FranchiseId == toFranchiseId &&
                b.CentralKitchenId == null &&
                b.IngredientId == ingredientId &&
                b.BatchCode == src.BatchCode, ct);

            if (dest is null)
            {
                dest = new IngredientBatch
                {
                    Type = InventoryOwnerType.Franchise,
                    FranchiseId = toFranchiseId,
                    CentralKitchenId = null,
                    IngredientId = ingredientId,
                    BatchCode = src.BatchCode,
                    Quantity = 0,
                    CreatedAt = src.CreatedAt
                };

                _db.IngredientBatches.Add(dest);
            }
            else if (dest.CreatedAt != src.CreatedAt)
            {
                throw new InvalidOperationException(
                    $"Ingredient batch age conflict for BatchCode={src.BatchCode} at destination franchise {toFranchiseId}.");
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

    private async Task TransferProductAsync(
        int deliveryId,
        int fromCentralKitchenId,
        int toFranchiseId,
        int productId,
        decimal requiredQty,
        DateTime now,
        CancellationToken ct)
    {
        if (requiredQty <= 0)
            throw new ArgumentException("Quantity must be > 0.");

        var product = await _db.Products
            .FirstOrDefaultAsync(x => x.ProductId == productId, ct);

        if (product is null)
            throw new KeyNotFoundException($"Product {productId} not found.");

        if (!string.Equals(product.Status, ProductStatus.Active, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Product {productId} is not ACTIVE.");

        // Vì ExpiredAt là derived => phải Include Product rồi sort in-memory
        var batches = await _db.ProductBatches
            .Include(b => b.Product)
            .Where(b =>
                b.CentralKitchenId == fromCentralKitchenId &&
                b.ProductId == productId &&
                b.Quantity > 0)
            .ToListAsync(ct);

        batches = batches
            .OrderBy(b => b.CalculateExpiredAt() == null)
            .ThenBy(b => b.CalculateExpiredAt())
            .ThenBy(b => b.CreatedAt)
            .ThenBy(b => b.BatchId)
            .ToList();

        var total = batches.Sum(b => b.Quantity);
        if (total < requiredQty)
            throw new InvalidOperationException(
                $"Insufficient product stock. ProductId={productId}, required={requiredQty}, available={total}");

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
                b.CentralKitchenId == null &&
                b.ProductId == productId &&
                b.BatchCode == src.BatchCode, ct);

            if (dest is null)
            {
                dest = new ProductBatch
                {
                    FranchiseId = toFranchiseId,
                    CentralKitchenId = null,
                    ProductId = productId,
                    BatchCode = src.BatchCode,
                    Quantity = 0,
                    CreatedAt = src.CreatedAt
                };

                _db.ProductBatches.Add(dest);
            }
            else if (dest.CreatedAt != src.CreatedAt)
            {
                throw new InvalidOperationException(
                    $"Product batch age conflict for BatchCode={src.BatchCode} at destination franchise {toFranchiseId}.");
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

    private async Task EnsureDeliveryScopeAsync(Delivery delivery, CancellationToken ct)
    {
        var planCentralKitchenId = await EnsurePlanScopeAsync(delivery.DeliveryPlan, ct);

        if (delivery.FromCentralKitchenId != planCentralKitchenId)
        {
            throw new InvalidOperationException(
                $"Delivery {delivery.DeliveryId} has inconsistent source central kitchen. planCentralKitchenId={planCentralKitchenId}, fromCentralKitchenId={delivery.FromCentralKitchenId}");
        }

        await _franchiseAccess.EnsureCanAccessCentralKitchenAsync(delivery.FromCentralKitchenId, ct);
        await _franchiseAccess.EnsureCanAccessAsync(delivery.DeliveryPlan.FranchiseId, ct);
    }

    private async Task<int> EnsurePlanScopeAsync(
        DeliveryPlan plan,
        CancellationToken ct,
        bool persistResolvedCentralKitchenId = false)
    {
        var franchiseCentralKitchenId = await _db.Franchises
            .AsNoTracking()
            .Where(x => x.FranchiseId == plan.FranchiseId)
            .Select(x => (int?)x.CentralKitchenId)
            .FirstOrDefaultAsync(ct);

        if (!franchiseCentralKitchenId.HasValue)
            throw new KeyNotFoundException($"Franchise {plan.FranchiseId} not found.");

        var resolvedCentralKitchenId = plan.CentralKitchenId ?? franchiseCentralKitchenId.Value;

        if (resolvedCentralKitchenId != franchiseCentralKitchenId.Value)
        {
            throw new InvalidOperationException(
                $"DeliveryPlan {plan.DeliveryPlanId} has inconsistent scope. planCentralKitchenId={plan.CentralKitchenId}, franchiseCentralKitchenId={franchiseCentralKitchenId.Value}");
        }

        if (!plan.CentralKitchenId.HasValue && persistResolvedCentralKitchenId)
        {
            plan.CentralKitchenId = resolvedCentralKitchenId;
            await _db.SaveChangesAsync(ct);
        }

        return resolvedCentralKitchenId;
    }

    private void RequireOneOf(params string[] roles)
    {
        var role = _current.Role;

        if (roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase)))
            return;

        throw new ForbiddenAccessException("You do not have permission for this action.");
    }

    private async Task AddAuditAsync(
        string action,
        string entityName,
        int entityId,
        int? franchiseId,
        object? oldObj,
        object? newObj,
        string? reason,
        CancellationToken ct)
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
}
