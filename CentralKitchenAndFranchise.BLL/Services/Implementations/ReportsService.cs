using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Reports;
using CentralKitchenAndFranchise.DTO.Responses.Reports;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class ReportsService : IReportsService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFranchiseAccessService _access;

    public ReportsService(
        AppDbContext db,
        ICurrentUserService current,
        IFranchiseAccessService access)
    {
        _db = db;
        _current = current;
        _access = access;
    }

    /// <summary>Build one inventory scope report from batch, movement, and adjustment audit data.</summary>
    public async Task<InventoryReportResponse> GetInventoryReportAsync(InventoryReportQuery query, CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff);

        var normalized = NormalizeDateRange(query.FromDate, query.ToDate, query.TimezoneOffsetMinutes);
        var scope = await ResolveInventoryScopeAsync(query.FranchiseId, query.CentralKitchenId, ct);

        var response = new InventoryReportResponse
        {
            FromDate = normalized.FromDate,
            ToDate = normalized.ToDate,
            TimezoneOffsetMinutes = normalized.TimezoneOffsetMinutes,
            ScopeType = scope.ScopeType,
            FranchiseId = scope.FranchiseId,
            FranchiseName = scope.FranchiseName,
            CentralKitchenId = scope.CentralKitchenId,
            CentralKitchenName = scope.CentralKitchenName
        };

        var ingredientBatches = await QueryIngredientBatchesForScope(scope, ct);
        var productBatches = await QueryProductBatchesForScope(scope, ct);

        var ingredientBatchIds = ingredientBatches.Select(x => x.BatchId).ToList();
        var productBatchIds = productBatches.Select(x => x.BatchId).ToList();

        var ingredientMovements = await QueryIngredientMovementsAsync(scope, normalized.ToUtcExclusive, ct);
        var productMovements = await QueryProductMovementsAsync(scope, normalized.ToUtcExclusive, ct);

        var ingredientAdjustments = await QuerySignedAdjustmentsAsync(
            entityName: nameof(IngredientBatch),
            scope,
            normalized.ToUtcExclusive,
            ct);

        var productAdjustments = await QuerySignedAdjustmentsAsync(
            entityName: nameof(ProductBatch),
            scope,
            normalized.ToUtcExclusive,
            ct);

        var ingredientItems = BuildIngredientInventoryRows(
            ingredientBatches,
            ingredientMovements,
            ingredientAdjustments,
            normalized.FromUtc,
            response.Notes);

        var productItems = await BuildProductInventoryRowsAsync(
            productBatches,
            productMovements,
            productAdjustments,
            normalized.FromUtc,
            response.Notes,
            ct);

        response.Items = ingredientItems
            .Concat(productItems)
            .OrderBy(x => x.ItemType)
            .ThenBy(x => x.ItemName)
            .ToList();

        if (response.Items.Count == 0)
        {
            response.Notes.Add("No inventory movements or batches were found in the selected scope/date window.");
        }

        return response;
    }

    /// <summary>Build ingredient wastage aggregates from inventory movement history.</summary>
    public async Task<WastageReportResponse> GetWastageReportAsync(WastageReportQuery query, CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff);

        var normalized = NormalizeDateRange(query.FromDate, query.ToDate, query.TimezoneOffsetMinutes);
        var sortBy = NormalizeWastageSortBy(query.SortBy);
        var scope = await ResolveWastageScopeAsync(query.FranchiseId, query.CentralKitchenId, ct);

        var response = new WastageReportResponse
        {
            FromDate = normalized.FromDate,
            ToDate = normalized.ToDate,
            TimezoneOffsetMinutes = normalized.TimezoneOffsetMinutes,
            ScopeType = scope.ScopeType,
            FranchiseId = scope.FranchiseId,
            FranchiseName = scope.FranchiseName,
            CentralKitchenId = scope.CentralKitchenId,
            CentralKitchenName = scope.CentralKitchenName,
            SortBy = sortBy
        };

        var movementQuery = _db.InventoryMovements
            .AsNoTracking()
            .Where(x => x.Batch != null)
            .Where(x => x.CreatedAt >= normalized.FromUtc && x.CreatedAt < normalized.ToUtcExclusive)
            .Where(x => x.Type == MovementType.Waste || x.Type == MovementType.Out);

        movementQuery = ApplyWastageScope(movementQuery, scope);

        var aggregates = await movementQuery
            .GroupBy(x => new
            {
                x.Batch!.IngredientId,
                IngredientName = x.Batch.Ingredient.Name,
                Unit = x.Batch.Ingredient.Unit,
                x.Batch.Ingredient.Price,
                WasteReason = x.Reason ?? "UNKNOWN"
            })
            .Select(g => new
            {
                g.Key.IngredientId,
                g.Key.IngredientName,
                g.Key.Unit,
                g.Key.Price,
                g.Key.WasteReason,
                WastedQuantity = g.Where(x => x.Type == MovementType.Waste).Sum(x => x.Quantity),
                OutboundQuantity = g.Where(x => x.Type == MovementType.Out).Sum(x => x.Quantity)
            })
            .ToListAsync(ct);

        var ingredientTotals = aggregates
            .GroupBy(x => x.IngredientId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Waste = g.Sum(x => x.WastedQuantity),
                    Out = g.Sum(x => x.OutboundQuantity)
                });

        response.Items = aggregates
            .Where(x => x.WastedQuantity > 0)
            .Select(x =>
            {
                var totals = ingredientTotals[x.IngredientId];
                var denominator = totals.Waste + totals.Out;
                var wasteRate = denominator <= 0
                    ? (decimal?)null
                    : Math.Round((totals.Waste / denominator) * 100m, 2);

                return new WastageReportItemResponse
                {
                    IngredientId = x.IngredientId,
                    IngredientName = x.IngredientName,
                    Unit = x.Unit,
                    WasteReason = x.WasteReason,
                    WastedQuantity = x.WastedQuantity,
                    WasteRate = wasteRate,
                    TotalLostValue = Math.Round(x.WastedQuantity * x.Price, 2)
                };
            })
            .ToList();

        response.Items = sortBy switch
        {
            "wastedQuantity" => response.Items
                .OrderByDescending(x => x.WastedQuantity)
                .ThenByDescending(x => x.TotalLostValue)
                .ThenBy(x => x.IngredientName)
                .ToList(),
            "wasteRate" => response.Items
                .OrderByDescending(x => x.WasteRate ?? 0m)
                .ThenByDescending(x => x.TotalLostValue)
                .ThenBy(x => x.IngredientName)
                .ToList(),
            _ => response.Items
                .OrderByDescending(x => x.TotalLostValue)
                .ThenByDescending(x => x.WastedQuantity)
                .ThenBy(x => x.IngredientName)
                .ToList()
        };

        if (response.Items.Count == 0)
        {
            response.Notes.Add("No ingredient waste movements were found in the selected scope/date window.");
        }

        response.Notes.Add("WasteRate uses the same denominator pattern as the current manager dashboard waste alert: waste / (waste + outbound). Returned value is a percentage.");
        response.Notes.Add("TotalLostValue uses current Ingredient.Price because the schema does not store historical ingredient price snapshots on movement rows.");

        return response;
    }

    /// <summary>Build per-store spending and delivery SLA metrics from order and delivery workflow data.</summary>
    public async Task<StorePerformanceReportResponse> GetStorePerformanceReportAsync(StorePerformanceReportQuery query, CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager);

        var normalized = NormalizeDateRange(query.FromDate, query.ToDate, query.TimezoneOffsetMinutes);

        var response = new StorePerformanceReportResponse
        {
            FromDate = normalized.FromDate,
            ToDate = normalized.ToDate,
            TimezoneOffsetMinutes = normalized.TimezoneOffsetMinutes
        };

        var franchises = await _db.Franchises
            .AsNoTracking()
            .Where(x => x.Status == OrganizationStatus.Active)
            .Select(x => new { x.FranchiseId, x.Name })
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        if (franchises.Count == 0)
        {
            response.Notes.Add("No active franchises were found.");
            return response;
        }

        var franchiseIds = franchises.Select(x => x.FranchiseId).ToList();

        var orderCounts = await _db.StoreOrders
            .AsNoTracking()
            .Where(x => franchiseIds.Contains(x.FranchiseId))
            .Where(x => x.OrderDate >= normalized.FromDate && x.OrderDate <= normalized.ToDate)
            .Where(x => x.Status != StoreOrderStatus.Draft && x.Status != StoreOrderStatus.Cancelled)
            .GroupBy(x => x.FranchiseId)
            .Select(g => new
            {
                FranchiseId = g.Key,
                TotalOrderCount = g.Count()
            })
            .ToDictionaryAsync(x => x.FranchiseId, x => x.TotalOrderCount, ct);

        var deliveredRows = await _db.Deliveries
            .AsNoTracking()
            .Where(x => x.DeliveryPlan.StoreOrderId.HasValue)
            .Where(x => franchiseIds.Contains(x.DeliveryPlan.FranchiseId))
            .Where(x => x.DeliveryPlan.PlannedDate >= normalized.FromDate && x.DeliveryPlan.PlannedDate <= normalized.ToDate)
            .Where(x => x.Status == DeliveryStatus.Delivered || x.Status == DeliveryStatus.Confirmed)
            .Where(x => x.DeliveredAt.HasValue)
            .Select(x => new
            {
                x.DeliveryId,
                FranchiseId = x.DeliveryPlan.FranchiseId,
                StoreOrderId = x.DeliveryPlan.StoreOrderId!.Value,
                x.DeliveryPlan.PlannedDate,
                x.DeliveredAt
            })
            .ToListAsync(ct);

        var deliveredMetrics = deliveredRows
            .GroupBy(x => x.FranchiseId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    TotalDeliveredOrders = g.Select(x => x.StoreOrderId).Distinct().Count(),
                    OnTimeDeliveredOrders = g
                        .Where(x => x.DeliveredAt.HasValue)
                        .GroupBy(x => x.StoreOrderId)
                        .Count(orderGroup =>
                        {
                            var firstDelivered = orderGroup
                                .Where(x => x.DeliveredAt.HasValue)
                                .OrderBy(x => x.DeliveredAt)
                                .First();

                            var deliveredLocalDate = DateOnly.FromDateTime(firstDelivered.DeliveredAt!.Value.AddMinutes(normalized.TimezoneOffsetMinutes));
                            return deliveredLocalDate <= firstDelivered.PlannedDate;
                        })
                });

        var deliveredDeliveryIds = deliveredRows.Select(x => x.DeliveryId).Distinct().ToList();

        var ingredientSpendByFranchise = await _db.DeliveryIngredientItems
            .AsNoTracking()
            .Where(x => deliveredDeliveryIds.Contains(x.DeliveryId))
            .GroupBy(x => new
            {
                FranchiseId = x.Delivery.DeliveryPlan.FranchiseId,
                x.IngredientId,
                Price = x.Ingredient.Price
            })
            .Select(g => new
            {
                g.Key.FranchiseId,
                Amount = g.Sum(x => x.Quantity) * g.Key.Price
            })
            .GroupBy(x => x.FranchiseId)
            .Select(g => new
            {
                FranchiseId = g.Key,
                Amount = g.Sum(x => x.Amount)
            })
            .ToDictionaryAsync(x => x.FranchiseId, x => Math.Round(x.Amount, 2), ct);

        var productSpendBase = await _db.DeliveryProductItems
            .AsNoTracking()
            .Where(x => deliveredDeliveryIds.Contains(x.DeliveryId))
            .Select(x => new
            {
                FranchiseId = x.Delivery.DeliveryPlan.FranchiseId,
                x.ProductId,
                x.Quantity
            })
            .ToListAsync(ct);

        var productPricingMap = await _db.StoreCatalogs
            .AsNoTracking()
            .Where(x => franchiseIds.Contains(x.FranchiseId))
            .ToDictionaryAsync(x => (x.FranchiseId, x.ProductId), x => x.Price, ct);

        var missingProductPrices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var productSpendByFranchise = productSpendBase
            .GroupBy(x => x.FranchiseId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    decimal amount = 0m;

                    foreach (var line in g)
                    {
                        if (productPricingMap.TryGetValue((line.FranchiseId, line.ProductId), out var price))
                        {
                            amount += line.Quantity * price;
                        }
                        else
                        {
                            missingProductPrices.Add($"FranchiseId={line.FranchiseId}, ProductId={line.ProductId}");
                        }
                    }

                    return Math.Round(amount, 2);
                });

        response.Items = franchises
            .Select(x =>
            {
                var totalDeliveredOrders = deliveredMetrics.TryGetValue(x.FranchiseId, out var deliveryMetric)
                    ? deliveryMetric.TotalDeliveredOrders
                    : 0;

                var onTimeDeliveredOrders = deliveredMetrics.TryGetValue(x.FranchiseId, out deliveryMetric)
                    ? deliveryMetric.OnTimeDeliveredOrders
                    : 0;

                var ingredientSpending = ingredientSpendByFranchise.TryGetValue(x.FranchiseId, out var ingAmount)
                    ? ingAmount
                    : 0m;

                var productSpending = productSpendByFranchise.TryGetValue(x.FranchiseId, out var prodAmount)
                    ? prodAmount
                    : 0m;

                return new StorePerformanceReportItemResponse
                {
                    FranchiseId = x.FranchiseId,
                    FranchiseName = x.Name,
                    TotalOrderCount = orderCounts.TryGetValue(x.FranchiseId, out var totalOrderCount)
                        ? totalOrderCount
                        : 0,
                    TotalIngredientSpending = ingredientSpending,
                    TotalProductSpending = productSpending,
                    TotalSpending = ingredientSpending + productSpending,
                    TotalDeliveredOrders = totalDeliveredOrders,
                    OnTimeDeliveredOrders = onTimeDeliveredOrders,
                    OnTimeRate = totalDeliveredOrders <= 0
                        ? null
                        : Math.Round((decimal)onTimeDeliveredOrders / totalDeliveredOrders * 100m, 2)
                };
            })
            .OrderByDescending(x => x.TotalSpending)
            .ThenByDescending(x => x.TotalDeliveredOrders)
            .ThenBy(x => x.FranchiseName)
            .ToList();

        response.Notes.Add("TotalOrderCount counts store orders inside the selected OrderDate window excluding DRAFT and CANCELLED statuses.");
        response.Notes.Add("Ingredient spending uses delivered ingredient quantity x current Ingredient.Price because the order/delivery schema does not persist historical ingredient price snapshots.");
        response.Notes.Add("Product spending uses delivered product quantity x current StoreCatalog.Price for the franchise because there is no historical product price snapshot on store-order or delivery lines.");
        response.Notes.Add("OnTimeRate follows the task formula: onTimeDeliveredOrders / totalDeliveredOrders x 100. The on-time predicate itself reuses the existing dashboard rule: DeliveredAt local date <= DeliveryPlan.PlannedDate.");

        if (missingProductPrices.Count > 0)
        {
            response.Notes.Add($"Some delivered product lines had no StoreCatalog price and were valued at 0: {string.Join("; ", missingProductPrices.OrderBy(x => x))}");
        }

        return response;
    }

    /// <summary>Resolve franchise or central-kitchen scope for inventory report access rules.</summary>
    private async Task<ReportScope> ResolveInventoryScopeAsync(int? franchiseId, int? centralKitchenId, CancellationToken ct)
    {
        if (_current.IsInRole(RoleNames.StoreStaff))
        {
            var assignedFranchiseId = await GetCurrentAssignedFranchiseIdAsync(ct);
            var targetFranchiseId = franchiseId ?? assignedFranchiseId;

            if (targetFranchiseId != assignedFranchiseId)
                throw new ForbiddenAccessException("StoreStaff can only view their assigned franchise inventory report.");

            await _access.EnsureCanAccessAsync(targetFranchiseId, ct);
            return await BuildFranchiseScopeAsync(targetFranchiseId, ct);
        }

        if (franchiseId.HasValue)
        {
            await _access.EnsureCanAccessAsync(franchiseId.Value, ct);
            return await BuildFranchiseScopeAsync(franchiseId.Value, ct);
        }

        var resolvedCentralKitchenId = await ResolveCentralKitchenIdAsync(centralKitchenId, ct);
        await _access.EnsureCanAccessCentralKitchenAsync(resolvedCentralKitchenId, ct);
        return await BuildCentralKitchenScopeAsync(resolvedCentralKitchenId, ct);
    }

    /// <summary>Resolve chain/store/kitchen scope for wastage report access rules.</summary>
    private async Task<ReportScope> ResolveWastageScopeAsync(int? franchiseId, int? centralKitchenId, CancellationToken ct)
    {
        if (_current.IsInRole(RoleNames.StoreStaff))
        {
            var assignedFranchiseId = await GetCurrentAssignedFranchiseIdAsync(ct);
            var targetFranchiseId = franchiseId ?? assignedFranchiseId;

            if (targetFranchiseId != assignedFranchiseId || centralKitchenId.HasValue)
                throw new ForbiddenAccessException("StoreStaff can only view wastage for their assigned franchise.");

            await _access.EnsureCanAccessAsync(targetFranchiseId, ct);
            return await BuildFranchiseScopeAsync(targetFranchiseId, ct);
        }

        if (franchiseId.HasValue)
        {
            await _access.EnsureCanAccessAsync(franchiseId.Value, ct);
            return await BuildFranchiseScopeAsync(franchiseId.Value, ct);
        }

        if (centralKitchenId.HasValue)
        {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId.Value, ct);
            return await BuildCentralKitchenScopeAsync(centralKitchenId.Value, ct);
        }

        return new ReportScope
        {
            ScopeType = "CHAIN"
        };
    }

    /// <summary>Read the current store user's assigned franchise id.</summary>
    private async Task<int> GetCurrentAssignedFranchiseIdAsync(CancellationToken ct)
    {
        var assignedFranchiseId = await _db.UserWorkAssignments
            .AsNoTracking()
            .Where(x =>
                x.UserId == _current.UserId &&
                x.AssignmentType == WorkAssignmentTypes.Franchise &&
                x.FranchiseId.HasValue)
            .OrderByDescending(x => x.AssignedAt)
            .ThenByDescending(x => x.UserWorkAssignmentId)
            .Select(x => x.FranchiseId)
            .FirstOrDefaultAsync(ct);

        if (!assignedFranchiseId.HasValue)
            throw new ForbiddenAccessException("Current user is not assigned to any franchise.");

        return assignedFranchiseId.Value;
    }

    /// <summary>Resolve one central kitchen when query input omitted it.</summary>
    private async Task<int> ResolveCentralKitchenIdAsync(int? centralKitchenId, CancellationToken ct)
    {
        if (centralKitchenId.HasValue)
            return centralKitchenId.Value;

        var centralKitchens = await _db.CentralKitchens
            .AsNoTracking()
            .Where(x => x.Status == OrganizationStatus.Active)
            .Select(x => new { x.CentralKitchenId })
            .ToListAsync(ct);

        if (centralKitchens.Count == 1)
            return centralKitchens[0].CentralKitchenId;

        throw new InvalidOperationException("CentralKitchenId is required when the system has multiple active central kitchens and franchiseId is not provided.");
    }

    /// <summary>Build a strongly typed franchise scope descriptor.</summary>
    private async Task<ReportScope> BuildFranchiseScopeAsync(int franchiseId, CancellationToken ct)
    {
        var franchise = await _db.Franchises
            .AsNoTracking()
            .Where(x => x.FranchiseId == franchiseId)
            .Select(x => new
            {
                x.FranchiseId,
                FranchiseName = x.Name,
                x.CentralKitchenId,
                CentralKitchenName = x.CentralKitchen.Name
            })
            .FirstOrDefaultAsync(ct);

        if (franchise is null)
            throw new KeyNotFoundException($"Franchise {franchiseId} not found.");

        return new ReportScope
        {
            ScopeType = "FRANCHISE",
            FranchiseId = franchise.FranchiseId,
            FranchiseName = franchise.FranchiseName,
            CentralKitchenId = franchise.CentralKitchenId,
            CentralKitchenName = franchise.CentralKitchenName
        };
    }

    /// <summary>Build a strongly typed central-kitchen scope descriptor.</summary>
    private async Task<ReportScope> BuildCentralKitchenScopeAsync(int centralKitchenId, CancellationToken ct)
    {
        var centralKitchen = await _db.CentralKitchens
            .AsNoTracking()
            .Where(x => x.CentralKitchenId == centralKitchenId)
            .Select(x => new
            {
                x.CentralKitchenId,
                CentralKitchenName = x.Name
            })
            .FirstOrDefaultAsync(ct);

        if (centralKitchen is null)
            throw new KeyNotFoundException($"CentralKitchen {centralKitchenId} not found.");

        return new ReportScope
        {
            ScopeType = "CENTRAL_KITCHEN",
            CentralKitchenId = centralKitchen.CentralKitchenId,
            CentralKitchenName = centralKitchen.CentralKitchenName
        };
    }

    /// <summary>Load ingredient batch master rows for the requested report scope.</summary>
    private async Task<List<IngredientBatchRow>> QueryIngredientBatchesForScope(ReportScope scope, CancellationToken ct)
    {
        var query = _db.IngredientBatches
            .AsNoTracking()
            .Include(x => x.Ingredient)
            .AsQueryable();

        query = scope.ScopeType switch
        {
            "FRANCHISE" => query.Where(x =>
                x.Type == InventoryOwnerType.Franchise &&
                x.FranchiseId == scope.FranchiseId &&
                x.CentralKitchenId == null),
            _ => query.Where(x =>
                x.Type == InventoryOwnerType.CentralKitchen &&
                x.CentralKitchenId == scope.CentralKitchenId &&
                x.FranchiseId == null)
        };

        return await query
            .Select(x => new IngredientBatchRow
            {
                BatchId = x.BatchId,
                IngredientId = x.IngredientId,
                IngredientName = x.Ingredient.Name,
                Unit = x.Ingredient.Unit,
                UnitCost = x.Ingredient.Price
            })
            .ToListAsync(ct);
    }

    /// <summary>Load product batch master rows for the requested report scope.</summary>
    private async Task<List<ProductBatchRow>> QueryProductBatchesForScope(ReportScope scope, CancellationToken ct)
    {
        var query = _db.ProductBatches
            .AsNoTracking()
            .Include(x => x.Product)
            .AsQueryable();

        query = scope.ScopeType switch
        {
            "FRANCHISE" => query.Where(x => x.FranchiseId == scope.FranchiseId && x.CentralKitchenId == null),
            _ => query.Where(x => x.CentralKitchenId == scope.CentralKitchenId && x.FranchiseId == null)
        };

        return await query
            .Select(x => new ProductBatchRow
            {
                BatchId = x.BatchId,
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                Unit = x.Product.Unit
            })
            .ToListAsync(ct);
    }

    /// <summary>Load ingredient movement rows up to the report end boundary.</summary>
    private async Task<List<MovementRow>> QueryIngredientMovementsAsync(ReportScope scope, DateTime toUtcExclusive, CancellationToken ct)
    {
        var query = _db.InventoryMovements
            .AsNoTracking()
            .Where(x => x.Batch != null)
            .Where(x => x.CreatedAt < toUtcExclusive)
            .AsQueryable();

        query = scope.ScopeType switch
        {
            "FRANCHISE" => query.Where(x =>
                x.Batch!.Type == InventoryOwnerType.Franchise &&
                x.Batch.FranchiseId == scope.FranchiseId &&
                x.Batch.CentralKitchenId == null),
            _ => query.Where(x =>
                x.Batch!.Type == InventoryOwnerType.CentralKitchen &&
                x.Batch.CentralKitchenId == scope.CentralKitchenId &&
                x.Batch.FranchiseId == null)
        };

        return await query
            .Select(x => new MovementRow
            {
                BatchId = x.BatchId,
                Type = x.Type,
                Quantity = x.Quantity,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);
    }

    /// <summary>Load product movement rows up to the report end boundary.</summary>
    private async Task<List<MovementRow>> QueryProductMovementsAsync(ReportScope scope, DateTime toUtcExclusive, CancellationToken ct)
    {
        var query = _db.ProductMovements
            .AsNoTracking()
            .Where(x => x.Batch != null)
            .Where(x => x.CreatedAt < toUtcExclusive)
            .AsQueryable();

        query = scope.ScopeType switch
        {
            "FRANCHISE" => query.Where(x => x.Batch!.FranchiseId == scope.FranchiseId && x.Batch.CentralKitchenId == null),
            _ => query.Where(x => x.Batch!.CentralKitchenId == scope.CentralKitchenId && x.Batch.FranchiseId == null)
        };

        return await query
            .Select(x => new MovementRow
            {
                BatchId = x.BatchId,
                Type = x.Type,
                Quantity = x.Quantity,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);
    }

    /// <summary>Load signed ADJUST deltas from audit logs because movement rows store ADJUST as absolute quantity only.</summary>
    private async Task<List<AdjustmentAuditRow>> QuerySignedAdjustmentsAsync(
        string entityName,
        ReportScope scope,
        DateTime toUtcExclusive,
        CancellationToken ct)
    {
        var actions = entityName == nameof(IngredientBatch)
            ? new[] { "INGREDIENT_ADJUST", "CK_INGREDIENT_ADJUST" }
            : new[] { "FRANCHISE_PRODUCT_ADJUST", "CK_PRODUCT_ADJUST" };

        var query = _db.AuditLogs
            .AsNoTracking()
            .Where(x => x.EntityName == entityName)
            .Where(x => x.EntityId.HasValue)
            .Where(x => actions.Contains(x.Action))
            .Where(x => x.CreatedAt < toUtcExclusive)
            .AsQueryable();

        query = scope.ScopeType switch
        {
            "FRANCHISE" => query.Where(x => x.FranchiseId == scope.FranchiseId),
            _ => query.Where(x => x.CentralKitchenId == scope.CentralKitchenId)
        };

        var logs = await query
            .Select(x => new
            {
                BatchId = x.EntityId!.Value,
                x.CreatedAt,
                x.NewDataJson
            })
            .ToListAsync(ct);

        return logs
            .Select(x => new AdjustmentAuditRow
            {
                BatchId = x.BatchId,
                CreatedAt = x.CreatedAt,
                DeltaQuantity = TryReadSignedAdjustmentDelta(x.NewDataJson)
            })
            .Where(x => x.DeltaQuantity != 0)
            .ToList();
    }

    /// <summary>Aggregate ingredient report rows from movement history and signed adjustments.</summary>
    private static List<InventoryReportItemResponse> BuildIngredientInventoryRows(
        IReadOnlyCollection<IngredientBatchRow> batches,
        IReadOnlyCollection<MovementRow> movements,
        IReadOnlyCollection<AdjustmentAuditRow> adjustments,
        DateTime fromUtc,
        List<string> notes)
    {
        var movementMap = movements
            .GroupBy(x => x.BatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var adjustmentMap = adjustments
            .GroupBy(x => x.BatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = batches
            .GroupBy(x => new { x.IngredientId, x.IngredientName, x.Unit, x.UnitCost })
            .Select(g =>
            {
                decimal opening = 0m;
                decimal inbound = 0m;
                decimal outbound = 0m;
                decimal wasted = 0m;
                decimal adjustment = 0m;

                foreach (var batch in g)
                {
                    var batchMovements = movementMap.TryGetValue(batch.BatchId, out var mvRows)
                        ? mvRows
                        : new List<MovementRow>();

                    var batchAdjustments = adjustmentMap.TryGetValue(batch.BatchId, out var adjRows)
                        ? adjRows
                        : new List<AdjustmentAuditRow>();

                    opening += CalculateNetQuantity(batchMovements, batchAdjustments, beforeUtc: fromUtc);
                    inbound += SumQuantity(batchMovements, MovementType.In, fromUtc, inclusiveEnd: null);
                    outbound += SumQuantity(batchMovements, MovementType.Out, fromUtc, inclusiveEnd: null);
                    wasted += SumQuantity(batchMovements, MovementType.Waste, fromUtc, inclusiveEnd: null);
                    adjustment += SumAdjustment(batchAdjustments, fromUtc, inclusiveEnd: null);
                }

                var closing = opening + inbound - outbound - wasted + adjustment;

                return new InventoryReportItemResponse
                {
                    ItemId = g.Key.IngredientId,
                    ItemName = g.Key.IngredientName,
                    Unit = g.Key.Unit,
                    ItemType = "INGREDIENT",
                    OpeningQuantity = Round(opening),
                    InboundQuantity = Round(inbound),
                    OutboundQuantity = Round(outbound),
                    WastedQuantity = Round(wasted),
                    AdjustmentQuantity = Round(adjustment),
                    ClosingQuantity = Round(closing),
                    ClosingValue = Round(closing * g.Key.UnitCost)
                };
            })
            .Where(x =>
                x.OpeningQuantity != 0 ||
                x.InboundQuantity != 0 ||
                x.OutboundQuantity != 0 ||
                x.WastedQuantity != 0 ||
                x.AdjustmentQuantity != 0 ||
                x.ClosingQuantity != 0)
            .ToList();

        if (rows.Any(x => x.AdjustmentQuantity != 0))
        {
            notes.Add("Inventory closing/opening includes signed ADJUST quantities recovered from audit logs because movement rows store ADJUST as absolute quantity only.");
        }

        return rows;
    }

    /// <summary>Aggregate product report rows and derive estimated product cost from active BOM data.</summary>
    private async Task<List<InventoryReportItemResponse>> BuildProductInventoryRowsAsync(
        IReadOnlyCollection<ProductBatchRow> batches,
        IReadOnlyCollection<MovementRow> movements,
        IReadOnlyCollection<AdjustmentAuditRow> adjustments,
        DateTime fromUtc,
        List<string> notes,
        CancellationToken ct)
    {
        var movementMap = movements
            .GroupBy(x => x.BatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var adjustmentMap = adjustments
            .GroupBy(x => x.BatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var productIds = batches.Select(x => x.ProductId).Distinct().ToList();
        var productUnitCostMap = await BuildEstimatedProductUnitCostMapAsync(productIds, ct);

        var missingUnitCostProducts = new HashSet<int>();

        var rows = batches
            .GroupBy(x => new { x.ProductId, x.ProductName, x.Unit })
            .Select(g =>
            {
                decimal opening = 0m;
                decimal inbound = 0m;
                decimal outbound = 0m;
                decimal wasted = 0m;
                decimal adjustment = 0m;

                foreach (var batch in g)
                {
                    var batchMovements = movementMap.TryGetValue(batch.BatchId, out var mvRows)
                        ? mvRows
                        : new List<MovementRow>();

                    var batchAdjustments = adjustmentMap.TryGetValue(batch.BatchId, out var adjRows)
                        ? adjRows
                        : new List<AdjustmentAuditRow>();

                    opening += CalculateNetQuantity(batchMovements, batchAdjustments, beforeUtc: fromUtc);
                    inbound += SumQuantity(batchMovements, MovementType.In, fromUtc, inclusiveEnd: null);
                    outbound += SumQuantity(batchMovements, MovementType.Out, fromUtc, inclusiveEnd: null);
                    wasted += SumQuantity(batchMovements, MovementType.Waste, fromUtc, inclusiveEnd: null);
                    adjustment += SumAdjustment(batchAdjustments, fromUtc, inclusiveEnd: null);
                }

                var closing = opening + inbound - outbound - wasted + adjustment;

                if (!productUnitCostMap.TryGetValue(g.Key.ProductId, out var unitCost))
                {
                    missingUnitCostProducts.Add(g.Key.ProductId);
                    unitCost = 0m;
                }

                return new InventoryReportItemResponse
                {
                    ItemId = g.Key.ProductId,
                    ItemName = g.Key.ProductName,
                    Unit = g.Key.Unit,
                    ItemType = "PRODUCT",
                    OpeningQuantity = Round(opening),
                    InboundQuantity = Round(inbound),
                    OutboundQuantity = Round(outbound),
                    WastedQuantity = Round(wasted),
                    AdjustmentQuantity = Round(adjustment),
                    ClosingQuantity = Round(closing),
                    ClosingValue = Round(closing * unitCost)
                };
            })
            .Where(x =>
                x.OpeningQuantity != 0 ||
                x.InboundQuantity != 0 ||
                x.OutboundQuantity != 0 ||
                x.WastedQuantity != 0 ||
                x.AdjustmentQuantity != 0 ||
                x.ClosingQuantity != 0)
            .ToList();

        if (rows.Any(x => x.AdjustmentQuantity != 0))
        {
            notes.Add("Product inventory opening/closing also includes signed ADJUST quantities recovered from audit logs.");
        }

        if (missingUnitCostProducts.Count > 0)
        {
            notes.Add($"Product closingValue uses estimated unit cost from latest ACTIVE BOM x current Ingredient.Price. Missing cost source defaulted to 0 for ProductIds: {string.Join(", ", missingUnitCostProducts.OrderBy(x => x))}.");
        }
        else if (rows.Any(x => x.ItemType == "PRODUCT"))
        {
            notes.Add("Product closingValue uses estimated unit cost from latest ACTIVE BOM x current Ingredient.Price because the schema does not store product cost snapshots.");
        }

        return rows;
    }

    /// <summary>Estimate current product unit cost from latest ACTIVE BOM and current ingredient prices.</summary>
    private async Task<Dictionary<int, decimal>> BuildEstimatedProductUnitCostMapAsync(List<int> productIds, CancellationToken ct)
    {
        if (productIds.Count == 0)
            return new Dictionary<int, decimal>();

        var boms = await _db.Boms
            .AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId) && x.Status == BomStatus.Active)
            .ToListAsync(ct);

        var latestBomByProduct = boms
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Version).First());

        var bomIds = latestBomByProduct.Values.Select(x => x.BomId).ToList();
        if (bomIds.Count == 0)
            return new Dictionary<int, decimal>();

        var bomItems = await _db.BomItems
            .AsNoTracking()
            .Where(x => bomIds.Contains(x.BomId))
            .Select(x => new
            {
                x.BomId,
                x.Quantity,
                IngredientPrice = x.Ingredient.Price
            })
            .ToListAsync(ct);

        var unitCostByBom = bomItems
            .GroupBy(x => x.BomId)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => x.Quantity * x.IngredientPrice), 4));

        return latestBomByProduct
            .Where(x => unitCostByBom.ContainsKey(x.Value.BomId))
            .ToDictionary(x => x.Key, x => unitCostByBom[x.Value.BomId]);
    }

    /// <summary>Apply scope rules to the wastage base query.</summary>
    private static IQueryable<InventoryMovement> ApplyWastageScope(IQueryable<InventoryMovement> query, ReportScope scope)
    {
        return scope.ScopeType switch
        {
            "FRANCHISE" => query.Where(x =>
                x.Batch!.Type == InventoryOwnerType.Franchise &&
                x.Batch.FranchiseId == scope.FranchiseId &&
                x.Batch.CentralKitchenId == null),
            "CENTRAL_KITCHEN" => query.Where(x =>
                x.Batch!.Type == InventoryOwnerType.CentralKitchen &&
                x.Batch.CentralKitchenId == scope.CentralKitchenId &&
                x.Batch.FranchiseId == null),
            _ => query.Where(x =>
                (x.Batch!.Type == InventoryOwnerType.Franchise && x.Batch.FranchiseId.HasValue) ||
                (x.Batch.Type == InventoryOwnerType.CentralKitchen && x.Batch.CentralKitchenId.HasValue))
        };
    }

    /// <summary>Normalize a report date range and build UTC boundaries from local business dates.</summary>
    private static NormalizedDateRange NormalizeDateRange(DateOnly fromDate, DateOnly toDate, int? timezoneOffsetMinutes)
    {
        var tz = timezoneOffsetMinutes ?? 420;
        if (tz is < -14 * 60 or > 14 * 60)
            throw new ArgumentException("timezoneOffsetMinutes must be between -840 and 840.");

        if (fromDate > toDate)
            throw new ArgumentException("fromDate must be <= toDate.");

        if (toDate.DayNumber - fromDate.DayNumber > 366)
            throw new ArgumentException("date range too large (max 366 days).");

        var fromUtc = DateTime.SpecifyKind(fromDate.ToDateTime(TimeOnly.MinValue).AddMinutes(-tz), DateTimeKind.Utc);
        var toUtcExclusive = DateTime.SpecifyKind(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue).AddMinutes(-tz), DateTimeKind.Utc);

        return new NormalizedDateRange
        {
            FromDate = fromDate,
            ToDate = toDate,
            TimezoneOffsetMinutes = tz,
            FromUtc = fromUtc,
            ToUtcExclusive = toUtcExclusive
        };
    }

    /// <summary>Normalize supported wastage sort fields.</summary>
    private static string NormalizeWastageSortBy(string? sortBy)
    {
        var value = (sortBy ?? "lostValue").Trim().ToLowerInvariant();
        return value switch
        {
            "lostvalue" => "lostValue",
            "wastedquantity" => "wastedQuantity",
            "wasterate" => "wasteRate",
            _ => throw new ArgumentException("sortBy must be one of: lostValue, wastedQuantity, wasteRate.")
        };
    }

    /// <summary>Convert movement rows before the boundary into signed net stock.</summary>
    private static decimal CalculateNetQuantity(
        IReadOnlyCollection<MovementRow> movements,
        IReadOnlyCollection<AdjustmentAuditRow> adjustments,
        DateTime beforeUtc)
    {
        var movementNet = movements
            .Where(x => x.CreatedAt < beforeUtc)
            .Sum(x => x.Type switch
            {
                var t when string.Equals(t, MovementType.In, StringComparison.OrdinalIgnoreCase) => x.Quantity,
                var t when string.Equals(t, MovementType.Out, StringComparison.OrdinalIgnoreCase) => -x.Quantity,
                var t when string.Equals(t, MovementType.Waste, StringComparison.OrdinalIgnoreCase) => -x.Quantity,
                _ => 0m
            });

        var adjustmentNet = adjustments
            .Where(x => x.CreatedAt < beforeUtc)
            .Sum(x => x.DeltaQuantity);

        return movementNet + adjustmentNet;
    }

    /// <summary>Sum one movement type in the selected in-range window.</summary>
    private static decimal SumQuantity(
        IReadOnlyCollection<MovementRow> movements,
        string type,
        DateTime fromUtc,
        DateTime? inclusiveEnd)
    {
        return movements
            .Where(x => x.CreatedAt >= fromUtc)
            .Where(x => !inclusiveEnd.HasValue || x.CreatedAt < inclusiveEnd.Value)
            .Where(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Quantity);
    }

    /// <summary>Sum signed adjustment delta in the selected in-range window.</summary>
    private static decimal SumAdjustment(
        IReadOnlyCollection<AdjustmentAuditRow> adjustments,
        DateTime fromUtc,
        DateTime? inclusiveEnd)
    {
        return adjustments
            .Where(x => x.CreatedAt >= fromUtc)
            .Where(x => !inclusiveEnd.HasValue || x.CreatedAt < inclusiveEnd.Value)
            .Sum(x => x.DeltaQuantity);
    }

    /// <summary>Parse signed adjustment delta from audit NewDataJson payload.</summary>
    private static decimal TryReadSignedAdjustmentDelta(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0m;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("Movement", out var movement) &&
                movement.ValueKind == JsonValueKind.Object &&
                movement.TryGetProperty("Delta", out var nestedDelta) &&
                nestedDelta.TryGetDecimal(out var nestedValue))
            {
                return nestedValue;
            }

            if (root.TryGetProperty("DeltaQuantity", out var delta) && delta.TryGetDecimal(out var value))
            {
                return value;
            }
        }
        catch
        {
            return 0m;
        }

        return 0m;
    }

    /// <summary>Round numeric report values consistently.</summary>
    private static decimal Round(decimal value) => Math.Round(value, 2);

    /// <summary>Enforce one of the supplied roles for the current user.</summary>
    private void RequireOneOf(params string[] roles)
    {
        var role = _current.Role;
        if (roles.Any(x => string.Equals(x, role, StringComparison.OrdinalIgnoreCase)))
            return;

        throw new ForbiddenAccessException("You do not have permission for this report.");
    }

    private sealed class ReportScope
    {
        public string ScopeType { get; set; } = default!;
        public int? FranchiseId { get; set; }
        public string? FranchiseName { get; set; }
        public int? CentralKitchenId { get; set; }
        public string? CentralKitchenName { get; set; }
    }

    private sealed class NormalizedDateRange
    {
        public DateOnly FromDate { get; set; }
        public DateOnly ToDate { get; set; }
        public int TimezoneOffsetMinutes { get; set; }
        public DateTime FromUtc { get; set; }
        public DateTime ToUtcExclusive { get; set; }
    }

    private sealed class IngredientBatchRow
    {
        public int BatchId { get; set; }
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = default!;
        public string Unit { get; set; } = default!;
        public decimal UnitCost { get; set; }
    }

    private sealed class ProductBatchRow
    {
        public int BatchId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = default!;
        public string Unit { get; set; } = default!;
    }

    private sealed class MovementRow
    {
        public int BatchId { get; set; }
        public string Type { get; set; } = default!;
        public decimal Quantity { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class AdjustmentAuditRow
    {
        public int BatchId { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal DeltaQuantity { get; set; }
    }
}
