using CentralKitchenAndFranchise.BLL.Extensions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class ManagerDashboardService : IManagerDashboardService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IDashboardScopeService _scopeService;
    private readonly IFranchiseAccessService _franchiseAccess;

    public ManagerDashboardService(
        AppDbContext db,
        ICurrentUserService current,
        IDashboardScopeService scopeService,
        IFranchiseAccessService franchiseAccess)
    {
        _db = db;
        _current = current;
        _scopeService = scopeService;
        _franchiseAccess = franchiseAccess;
    }

    /// <summary>Build the manager dashboard across all active franchises in scope.</summary>
    public async Task<ManagerDashboardOverviewResponse> GetOverviewAsync(ManagerDashboardOverviewQuery query, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        query ??= new ManagerDashboardOverviewQuery();

        var (fromDate, toDate, tzOffsetMinutes, limit, fromUtc, toUtcExclusive, todayLocal) = NormalizeQuery(query);
        var scopeFranchiseIds = await _scopeService.GetAllActiveFranchiseIdsAsync(ct);

        var response = new ManagerDashboardOverviewResponse
        {
            FromDate = fromDate,
            ToDate = toDate,
            TimezoneOffsetMinutes = tzOffsetMinutes,
            FranchiseCount = scopeFranchiseIds.Count
        };

        if (scopeFranchiseIds.Count == 0)
        {
            response.Notes.Add("There are no active franchises in scope.");
            return response;
        }

        await FillOrderSummaryAsync(response, scopeFranchiseIds, fromDate, toDate, ct);
        await FillDeliverySummaryAsync(response, scopeFranchiseIds, fromDate, toDate, ct);
        await FillServiceLevelAsync(response, scopeFranchiseIds, fromDate, toDate, tzOffsetMinutes, ct);
        await FillLowStockAlertsAsync(response, scopeFranchiseIds, limit, ct);
        await FillNearExpiryAlertsAsync(response, scopeFranchiseIds, limit, todayLocal, ct);
        await FillWasteAlertsAsync(response, scopeFranchiseIds, fromUtc, toUtcExclusive, limit, ct);

        return response;
    }

    /// <summary>Build the manager dashboard for one specific active franchise.</summary>
    public async Task<ManagerDashboardOverviewResponse> GetFranchiseOverviewAsync(int franchiseId, ManagerDashboardOverviewQuery query, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        await _franchiseAccess.EnsureCanAccessAsync(franchiseId, ct);

        query ??= new ManagerDashboardOverviewQuery();
        var (fromDate, toDate, tzOffsetMinutes, limit, fromUtc, toUtcExclusive, todayLocal) = NormalizeQuery(query);

        var scope = await _db.Franchises
            .AsNoTracking()
            .Where(x => x.FranchiseId == franchiseId && x.Status == OrganizationStatus.Active)
            .Select(x => new { x.FranchiseId })
            .FirstOrDefaultAsync(ct);

        if (scope is null)
            throw new KeyNotFoundException("Franchise not found.");

        var response = new ManagerDashboardOverviewResponse
        {
            FromDate = fromDate,
            ToDate = toDate,
            TimezoneOffsetMinutes = tzOffsetMinutes,
            FranchiseCount = 1
        };

        var franchiseIds = new List<int> { franchiseId };

        await FillOrderSummaryAsync(response, franchiseIds, fromDate, toDate, ct);
        await FillDeliverySummaryAsync(response, franchiseIds, fromDate, toDate, ct);
        await FillServiceLevelAsync(response, franchiseIds, fromDate, toDate, tzOffsetMinutes, ct);
        await FillLowStockAlertsAsync(response, franchiseIds, limit, ct);
        await FillNearExpiryAlertsAsync(response, franchiseIds, limit, todayLocal, ct);
        await FillWasteAlertsAsync(response, franchiseIds, fromUtc, toUtcExclusive, limit, ct);

        return response;
    }

    /// <summary>Enforce the Admin/Manager-only permission for this dashboard.</summary>
    private void RequireAdminOrManager()
    {
        if (_current.IsInRole(RoleNames.Admin) || _current.IsInRole(RoleNames.Manager))
            return;

        throw new UnauthorizedAccessException("Only Admin or Manager can access this dashboard.");
    }

    /// <summary>Normalize dashboard filters and clamp list sizes.</summary>
    private static (DateOnly fromDate, DateOnly toDate, int timezoneOffsetMinutes, int limit, DateTime fromUtc, DateTime toUtcExclusive, DateOnly todayLocal) NormalizeQuery(ManagerDashboardOverviewQuery query)
    {
        var tz = query.TimezoneOffsetMinutes ?? 0;
        if (tz is < -14 * 60 or > 14 * 60)
            throw new ArgumentException("timezoneOffsetMinutes must be between -840 and 840.");

        var todayLocal = DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(tz));
        var toDate = query.ToDate ?? todayLocal;
        var fromDate = query.FromDate ?? toDate.AddDays(-6);

        if (fromDate > toDate)
            throw new ArgumentException("fromDate must be <= toDate.");

        if (toDate.DayNumber - fromDate.DayNumber > 92)
            throw new ArgumentException("date range too large (max 92 days).");

        var limit = query.Limit <= 0 ? 20 : query.Limit;
        if (limit > 200) limit = 200;

        var fromUtc = DateTime.SpecifyKind(fromDate.ToDateTime(TimeOnly.MinValue).AddMinutes(-tz), DateTimeKind.Utc);
        var toUtcExclusive = DateTime.SpecifyKind(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue).AddMinutes(-tz), DateTimeKind.Utc);

        return (fromDate, toDate, tz, limit, fromUtc, toUtcExclusive, todayLocal);
    }

    /// <summary>Aggregate order statuses using StoreOrder.OrderDate as business date.</summary>
    private async Task FillOrderSummaryAsync(
        ManagerDashboardOverviewResponse response,
        List<int> franchiseIds,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct)
    {
        var rows = await _db.StoreOrders
            .AsNoTracking()
            .Where(x => franchiseIds.Contains(x.FranchiseId))
            .Where(x => x.OrderDate >= fromDate && x.OrderDate <= toDate)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        response.OrderStatusSummary.Total = rows.Sum(x => x.Count);
        response.OrderStatusSummary.ByStatus = rows
            .OrderByDescending(x => x.Count)
            .ToDictionary(x => x.Status ?? "UNKNOWN", x => x.Count, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Aggregate delivery and receiving statuses using DeliveryPlan.PlannedDate as business date.</summary>
    private async Task FillDeliverySummaryAsync(
        ManagerDashboardOverviewResponse response,
        List<int> franchiseIds,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct)
    {
        var rows = await _db.Deliveries
            .AsNoTracking()
            .Where(x => franchiseIds.Contains(x.DeliveryPlan.FranchiseId))
            .Where(x => x.DeliveryPlan.PlannedDate >= fromDate && x.DeliveryPlan.PlannedDate <= toDate)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        response.DeliveryStatusSummary.Total = rows.Sum(x => x.Count);
        response.DeliveryStatusSummary.ByStatus = rows
            .OrderByDescending(x => x.Count)
            .ToDictionary(x => x.Status ?? "UNKNOWN", x => x.Count, StringComparer.OrdinalIgnoreCase);

        response.DeliveryStatusSummary.DeliveredCount = rows
            .Where(x => IsDeliveredOrConfirmed(x.Status))
            .Sum(x => x.Count);

        response.DeliveryStatusSummary.PendingCount = rows
            .Where(x =>
                string.Equals(x.Status, DeliveryStatus.Created, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, DeliveryStatus.Shipped, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Count);

        response.DeliveryStatusSummary.DeliveredPendingReceivingCount = await _db.Deliveries
            .AsNoTracking()
            .Where(x => franchiseIds.Contains(x.DeliveryPlan.FranchiseId))
            .Where(x => x.DeliveryPlan.PlannedDate >= fromDate && x.DeliveryPlan.PlannedDate <= toDate)
            .Where(x => x.Status == DeliveryStatus.Delivered && !x.ReceivingReports.Any())
            .CountAsync(ct);

        response.DeliveryStatusSummary.ConfirmedReceivingCount = await _db.Deliveries
            .AsNoTracking()
            .Where(x => franchiseIds.Contains(x.DeliveryPlan.FranchiseId))
            .Where(x => x.DeliveryPlan.PlannedDate >= fromDate && x.DeliveryPlan.PlannedDate <= toDate)
            .Where(x => x.ReceivingReports.Any())
            .CountAsync(ct);
    }

    /// <summary>Compute on-time delivery rate against the selected planned-date window.</summary>
    private async Task FillServiceLevelAsync(
        ManagerDashboardOverviewResponse response,
        List<int> franchiseIds,
        DateOnly fromDate,
        DateOnly toDate,
        int timezoneOffsetMinutes,
        CancellationToken ct)
    {
        var deliveries = await _db.Deliveries
            .AsNoTracking()
            .Where(x => franchiseIds.Contains(x.DeliveryPlan.FranchiseId))
            .Where(x => x.DeliveryPlan.PlannedDate >= fromDate && x.DeliveryPlan.PlannedDate <= toDate)
            .Select(x => new
            {
                PlannedDate = x.DeliveryPlan.PlannedDate,
                x.Status,
                x.DeliveredAt
            })
            .ToListAsync(ct);

        response.ServiceLevelSummary.TotalDeliveriesPlannedInRange = deliveries.Count;

        if (deliveries.Count == 0)
        {
            response.ServiceLevelSummary.TotalDeliveriesDeliveredInRange = 0;
            response.ServiceLevelSummary.OnTimeDeliveredCount = 0;
            response.ServiceLevelSummary.OnTimeRate = null;
            response.Notes.Add("SERVICE_LEVEL: No deliveries in selected date range.");
            return;
        }

        var delivered = deliveries
            .Where(x => IsDeliveredOrConfirmed(x.Status) && x.DeliveredAt.HasValue)
            .ToList();

        var onTimeDeliveredCount = delivered.Count(x =>
            DateOnly.FromDateTime(x.DeliveredAt!.Value.AddMinutes(timezoneOffsetMinutes)) <= x.PlannedDate);

        response.ServiceLevelSummary.TotalDeliveriesDeliveredInRange = delivered.Count;
        response.ServiceLevelSummary.OnTimeDeliveredCount = onTimeDeliveredCount;
        response.ServiceLevelSummary.OnTimeRate = deliveries.Count == 0
            ? null
            : Math.Round((decimal)onTimeDeliveredCount / deliveries.Count, 4);
    }

    /// <summary>Return low-stock ingredient alerts across all franchises in scope.</summary>
    private async Task FillLowStockAlertsAsync(
        ManagerDashboardOverviewResponse response,
        List<int> franchiseIds,
        int limit,
        CancellationToken ct)
    {
        var lowStockRows = await _db.IngredientBatches
             .AsNoTracking()
             .Where(x =>
                 x.Type == InventoryOwnerType.Franchise &&
                 x.FranchiseId.HasValue &&
                 x.CentralKitchenId == null &&
                 !x.IsInTransit &&
                 x.DeliveryId == null &&
                 franchiseIds.Contains(x.FranchiseId.Value))
             .Where(x => x.Ingredient.Status == IngredientStatus.Active)
             .GroupBy(x => new
             {
                 FranchiseId = x.FranchiseId!.Value,
                 x.IngredientId
             })
             .Select(g => new
             {
                 g.Key.FranchiseId,
                 g.Key.IngredientId,
                 OnHand = g.Sum(x => x.Quantity)
             })
             .Join(
                 _db.Ingredients.AsNoTracking().Where(x => x.Status == IngredientStatus.Active),
                 x => x.IngredientId,
                 i => i.IngredientId,
                 (x, i) => new
                 {
                     x.FranchiseId,
                     x.IngredientId,
                     x.OnHand,
                     i.SafetyStock,
                     i.Name,
                     i.Unit
                 })
             .Where(x => x.SafetyStock > 0 && x.OnHand < x.SafetyStock)
             .OrderBy(x => x.OnHand / x.SafetyStock)
             .ThenBy(x => x.Name)
             .Take(limit)
             .ToListAsync(ct);

        var franchiseNameMap = await _db.Franchises
            .AsNoTracking()
            .Where(x => franchiseIds.Contains(x.FranchiseId))
            .ToDictionaryAsync(x => x.FranchiseId, x => x.Name, ct);

        response.LowStockAlerts = lowStockRows
            .Select(x => new LowStockAlertItem
            {
                FranchiseId = x.FranchiseId,
                FranchiseName = franchiseNameMap.TryGetValue(x.FranchiseId, out var franchiseName)
                    ? franchiseName
                    : $"Franchise #{x.FranchiseId}",
                IngredientId = x.IngredientId,
                IngredientName = x.Name,
                Unit = x.Unit,
                OnHandQuantity = x.OnHand,
                SafetyStock = x.SafetyStock
            })
            .ToList();
    }

    /// <summary>Return near-expiry ingredient batch alerts across all franchises in scope.</summary>
    private async Task FillNearExpiryAlertsAsync(
        ManagerDashboardOverviewResponse response,
        List<int> franchiseIds,
        int limit,
        DateOnly todayLocal,
        CancellationToken ct)
    {
        var nearExpiryDays = await GetNearExpiryDaysAsync(ct);
        if (nearExpiryDays <= 0)
        {
            response.Notes.Add("NEAR_EXPIRY: System setting NEAR_EXPIRY_DAYS is not configured or invalid.");
            return;
        }

        var cutoff = todayLocal.AddDays(nearExpiryDays);

        var batches = await _db.IngredientBatches
            .AsNoTracking()
            .Include(x => x.Ingredient)
            .Where(x =>
                x.Type == InventoryOwnerType.Franchise &&
                x.FranchiseId.HasValue &&
                x.CentralKitchenId == null &&
                !x.IsInTransit &&
                x.DeliveryId == null &&
                franchiseIds.Contains(x.FranchiseId.Value) &&
                x.Quantity > 0)
            .ToListAsync(ct);

        var franchiseNameMap = await _db.Franchises
            .AsNoTracking()
            .Where(x => franchiseIds.Contains(x.FranchiseId))
            .ToDictionaryAsync(x => x.FranchiseId, x => x.Name, ct);

        response.NearExpiryAlerts = batches
            .Select(x => new
            {
                FranchiseId = x.FranchiseId!.Value,
                x.IngredientId,
                IngredientName = x.Ingredient.Name,
                x.Ingredient.Unit,
                x.BatchId,
                x.BatchCode,
                x.Quantity,
                ExpiredAt = x.CalculateExpiredAt()
            })
            .Where(x => x.ExpiredAt != null && x.ExpiredAt <= cutoff)
            .OrderBy(x => x.ExpiredAt)
            .ThenByDescending(x => x.Quantity)
            .Take(limit)
            .Select(x => new NearExpiryAlertItem
            {
                FranchiseId = x.FranchiseId,
                FranchiseName = franchiseNameMap.TryGetValue(x.FranchiseId, out var franchiseName)
                    ? franchiseName
                    : $"Franchise #{x.FranchiseId}",
                IngredientId = x.IngredientId,
                IngredientName = x.IngredientName,
                Unit = x.Unit,
                BatchId = x.BatchId,
                BatchCode = x.BatchCode,
                Quantity = x.Quantity,
                ExpiredAt = x.ExpiredAt,
                DaysToExpire = x.ExpiredAt?.DayNumber - todayLocal.DayNumber
            })
            .ToList();
    }

    /// <summary>Return waste-rate alerts using movement data inside the selected date window.</summary>
    private async Task FillWasteAlertsAsync(
        ManagerDashboardOverviewResponse response,
        List<int> franchiseIds,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        int limit,
        CancellationToken ct)
    {
        var movements = await _db.InventoryMovements
            .AsNoTracking()
            .Where(x =>
                x.Batch != null &&
                x.Batch.Type == InventoryOwnerType.Franchise &&
                x.Batch.FranchiseId.HasValue &&
                franchiseIds.Contains(x.Batch.FranchiseId.Value) &&
                x.CreatedAt >= fromUtc &&
                x.CreatedAt < toUtcExclusive &&
                (x.Type == MovementType.Waste || x.Type == MovementType.Out))
            .GroupBy(x => new
            {
                FranchiseId = x.Batch!.FranchiseId!.Value,
                x.Batch.IngredientId
            })
            .Select(g => new
            {
                g.Key.FranchiseId,
                g.Key.IngredientId,
                Waste = g.Where(x => x.Type == MovementType.Waste).Sum(x => x.Quantity),
                Out = g.Where(x => x.Type == MovementType.Out).Sum(x => x.Quantity)
            })
            .ToListAsync(ct);

        if (movements.Count == 0)
        {
            response.Notes.Add("WASTE: No inventory movements (OUT/WASTE) in selected date range.");
            return;
        }

        var ingredientMap = await _db.Ingredients
            .AsNoTracking()
            .Where(x => x.Status == IngredientStatus.Active)
            .Select(x => new
            {
                x.IngredientId,
                x.Name,
                x.Unit,
                x.WasteThreshold
            })
            .ToDictionaryAsync(x => x.IngredientId, x => x, ct);

        var franchiseMap = await _db.Franchises
            .AsNoTracking()
            .Where(x => franchiseIds.Contains(x.FranchiseId))
            .ToDictionaryAsync(x => x.FranchiseId, x => x.Name, ct);

        response.WasteAlerts = movements
            .Where(x => ingredientMap.ContainsKey(x.IngredientId))
            .Select(x =>
            {
                var ingredient = ingredientMap[x.IngredientId];
                var denominator = x.Waste + x.Out;
                var wasteRate = denominator <= 0
                    ? (decimal?)null
                    : Math.Round(x.Waste / denominator, 6);
                var isExceed = wasteRate.HasValue && ingredient.WasteThreshold > 0 && wasteRate.Value > ingredient.WasteThreshold;

                return new WasteAlertItem
                {
                    FranchiseId = x.FranchiseId,
                    FranchiseName = franchiseMap.TryGetValue(x.FranchiseId, out var franchiseName)
                        ? franchiseName
                        : $"Franchise #{x.FranchiseId}",
                    IngredientId = x.IngredientId,
                    IngredientName = ingredient.Name,
                    Unit = ingredient.Unit,
                    WasteQuantity = x.Waste,
                    IssuedQuantity = x.Out,
                    WasteRate = wasteRate,
                    WasteThreshold = ingredient.WasteThreshold,
                    IsExceedThreshold = isExceed
                };
            })
            .OrderByDescending(x => x.IsExceedThreshold)
            .ThenByDescending(x => x.WasteRate ?? 0m)
            .ThenByDescending(x => x.WasteQuantity)
            .Take(limit)
            .ToList();
    }

    /// <summary>Read the configured near-expiry window from system settings.</summary>
    private async Task<int> GetNearExpiryDaysAsync(CancellationToken ct)
    {
        var raw = await _db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key == SettingKeys.NearExpiryDays)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);

        return int.TryParse(raw, out var days) ? days : 0;
    }

    private static bool IsDeliveredOrConfirmed(string? status)
    {
        return string.Equals(status, DeliveryStatus.Delivered, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, DeliveryStatus.Confirmed, StringComparison.OrdinalIgnoreCase);
    }
}