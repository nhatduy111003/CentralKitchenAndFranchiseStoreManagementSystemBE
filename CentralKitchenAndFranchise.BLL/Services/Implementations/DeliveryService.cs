using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Extensions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Deliveries;
using CentralKitchenAndFranchise.DTO.Responses.Deliveries;
using CentralKitchenAndFranchise.DTO.Responses.Inventory;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
            DeliveredAt = null
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

        var shippedProductBatchMap = await LoadShippedProductBatchMapAsync(
            delivery.DeliveryId,
            delivery.DeliveryPlan.FranchiseId,
            productIds,
            ct);

        var shippedIngredientBatchMap = await LoadShippedIngredientBatchMapAsync(
            delivery.DeliveryId,
            delivery.DeliveryPlan.FranchiseId,
            ingredientIds,
            ct);

        var availableProductQtyMap = availableProductBatchMap
            .ToDictionary(x => x.Key, x => x.Value.Sum(b => b.Quantity));

        var availableIngredientQtyMap = availableIngredientBatchMap
            .ToDictionary(x => x.Key, x => x.Value.Sum(b => b.Quantity));

        var shippedProductQtyMap = shippedProductBatchMap
            .ToDictionary(x => x.Key, x => x.Value.Sum(b => b.Quantity));

        var shippedIngredientQtyMap = shippedIngredientBatchMap
            .ToDictionary(x => x.Key, x => x.Value.Sum(b => b.Quantity));

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
            ProductItems = delivery.ProductItems.Select(x =>
            {
                var requestedQuantity = x.RequestedQuantity > 0 ? x.RequestedQuantity : x.Quantity;

                return new DeliveryProductItemDto
                {
                    ProductId = x.ProductId,
                    ProductName = x.Product.Name,
                    Quantity = x.Quantity,
                    RequestedQuantity = requestedQuantity,
                    DroppedQuantity = Math.Max(requestedQuantity - x.Quantity, 0m),
                    IsDropped = x.IsDropped,
                    DropReason = x.DropReason,
                    AvailableInCentralKitchenQuantity = GetTotalQuantity(availableProductQtyMap, x.ProductId),
                    AvailableCentralKitchenBatches = GetBatchList(availableProductBatchMap, x.ProductId),
                    ShippedToFranchiseQuantity = GetTotalQuantity(shippedProductQtyMap, x.ProductId),
                    ShippedToFranchiseBatches = GetBatchList(shippedProductBatchMap, x.ProductId)
                };
            }).ToList(),
            IngredientItems = delivery.IngredientItems.Select(x => new DeliveryIngredientItemDto
            {
                IngredientId = x.IngredientId,
                IngredientName = x.Ingredient.Name,
                Quantity = x.Quantity,
                AvailableInCentralKitchenQuantity = GetTotalQuantity(availableIngredientQtyMap, x.IngredientId),
                AvailableCentralKitchenBatches = GetBatchList(availableIngredientBatchMap, x.IngredientId),
                ShippedToFranchiseQuantity = GetTotalQuantity(shippedIngredientQtyMap, x.IngredientId),
                ShippedToFranchiseBatches = GetBatchList(shippedIngredientBatchMap, x.IngredientId)
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
                    Quantity = req.Quantity,
                    RequestedQuantity = req.Quantity,
                    IsDropped = false,
                    DropReason = null
                });
            }
            else
            {
                line.Quantity = req.Quantity;
                line.RequestedQuantity = req.Quantity;
                line.IsDropped = false;
                line.DropReason = null;
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

    public async Task<DeliveryDetailsResponse> ConfirmAsync(int deliveryId, CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.SupplyCoordinator);

        var delivery = await _db.Deliveries
            .Include(d => d.DeliveryPlan)
            .FirstOrDefaultAsync(d => d.DeliveryId == deliveryId, ct);

        if (delivery is null)
            throw new KeyNotFoundException($"Delivery {deliveryId} not found.");

        await EnsureDeliveryScopeAsync(delivery, ct);

        if (delivery.Status != DeliveryStatus.Created && delivery.Status != DeliveryStatus.Shipped)
            throw new InvalidOperationException("Only CREATED/SHIPPED deliveries can be marked as DELIVERED.");

        var now = DateTime.UtcNow;

        delivery.Status = DeliveryStatus.Delivered;
        delivery.DeliveredAt = now;

        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(deliveryId, ct);
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

    private async Task<Dictionary<int, List<InventoryBatchQuantityResponse>>> LoadShippedProductBatchMapAsync(
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

    private async Task<Dictionary<int, List<InventoryBatchQuantityResponse>>> LoadShippedIngredientBatchMapAsync(
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
