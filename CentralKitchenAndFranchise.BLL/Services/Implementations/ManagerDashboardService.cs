using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.Enums;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class ManagerDashboardService : IManagerDashboardService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFranchiseAccessService _franchiseAccess;

    public ManagerDashboardService(
        AppDbContext db,
        ICurrentUserService current,
        IFranchiseAccessService franchiseAccess)
    {
        _db = db;
        _current = current;
        _franchiseAccess = franchiseAccess;
    }

    public async Task<ManagerDashboardOverviewResponse> GetOverviewAsync(
        ManagerDashboardOverviewQuery query,
        CancellationToken ct = default)
    {
        RequireAdminOrManager();
        query ??= new ManagerDashboardOverviewQuery();

        var (fromDate, toDate, tzOffsetMinutes, limit, fromUtc, toUtcExclusive, todayLocal) =
            await NormalizeQueryAsync(query, ct);

        // Scope:
        // - Admin: all ACTIVE franchises
        // - Manager: only assigned ACTIVE franchises
        var scopeFranchiseIds = await GetScopeFranchiseIdsAsync(ct);

        var resp = new ManagerDashboardOverviewResponse
        {
            FromDate = fromDate,
            ToDate = toDate,
            TimezoneOffsetMinutes = tzOffsetMinutes,
            FranchiseCount = scopeFranchiseIds.Count
        };

        if (scopeFranchiseIds.Count == 0)
        {
            resp.Notes.Add("NO_FRANCHISE_SCOPE: Manager has no assigned franchises in user_franchises.");
            return resp;
        }

        await FillOrderSummaryAsync(resp, scopeFranchiseIds, fromUtc, toUtcExclusive, ct);
        await FillDeliverySummaryAsync(resp, scopeFranchiseIds, fromUtc, toUtcExclusive, ct);
        await FillServiceLevelAsync(resp, scopeFranchiseIds, fromUtc, toUtcExclusive, tzOffsetMinutes, ct);

        await FillLowStockAlertsAsync(resp, scopeFranchiseIds, limit, ct);
        await FillNearExpiryAlertsAsync(resp, scopeFranchiseIds, limit, todayLocal, ct);
        await FillWasteAlertsAsync(resp, scopeFranchiseIds, fromUtc, toUtcExclusive, limit, ct);

        return resp;
    }

    public async Task<ManagerDashboardOverviewResponse> GetFranchiseOverviewAsync(
        int franchiseId,
        ManagerDashboardOverviewQuery query,
        CancellationToken ct = default)
    {
        RequireAdminOrManager();

        await _franchiseAccess.EnsureCanAccessAsync(franchiseId, ct);

        query ??= new ManagerDashboardOverviewQuery();
        var (fromDate, toDate, tzOffsetMinutes, limit, fromUtc, toUtcExclusive, todayLocal) =
            await NormalizeQueryAsync(query, ct);

        var franchise = await _db.Franchises
            .AsNoTracking()
            .Where(f => f.FranchiseId == franchiseId && f.Status == OrganizationStatus.Active)
            .Select(f => new { f.FranchiseId, f.Name })
            .FirstOrDefaultAsync(ct);

        if (franchise is null)
            throw new KeyNotFoundException("Franchise not found.");

        var resp = new ManagerDashboardOverviewResponse
        {
            FromDate = fromDate,
            ToDate = toDate,
            TimezoneOffsetMinutes = tzOffsetMinutes,
            FranchiseCount = 1
        };

        var scope = new List<int> { franchiseId };

        await FillOrderSummaryAsync(resp, scope, fromUtc, toUtcExclusive, ct);
        await FillDeliverySummaryAsync(resp, scope, fromUtc, toUtcExclusive, ct);
        await FillServiceLevelAsync(resp, scope, fromUtc, toUtcExclusive, tzOffsetMinutes, ct);

        await FillLowStockAlertsAsync(resp, scope, limit, ct);
        await FillNearExpiryAlertsAsync(resp, scope, limit, todayLocal, ct);
        await FillWasteAlertsAsync(resp, scope, fromUtc, toUtcExclusive, limit, ct);

        return resp;
    }

    private void RequireAdminOrManager()
    {
        if (!_current.IsInRole(RoleNames.Admin) && !_current.IsInRole(RoleNames.Manager))
            throw new UnauthorizedAccessException("You do not have permission to access this resource.");
    }

    private async Task<List<int>> GetScopeFranchiseIdsAsync(CancellationToken ct)
    {
        if (_current.IsInRole(RoleNames.Admin) || _current.IsInRole(RoleNames.Manager))
        {
            return await _db.Franchises
                .AsNoTracking()
                .Where(f => f.Status == OrganizationStatus.Active)
                .Select(f => f.FranchiseId)
                .ToListAsync(ct);
        }

        return new List<int>();
    }
    private async Task<(
        DateOnly fromDate,
        DateOnly toDate,
        int tzOffsetMinutes,
        int limit,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        DateOnly todayLocal)> NormalizeQueryAsync(
        ManagerDashboardOverviewQuery query,
        CancellationToken ct)
    {
        await Task.CompletedTask;

        var tz = query.TimezoneOffsetMinutes ?? 0;
        if (tz is < -720 or > 840)
            throw new ArgumentException("timezoneOffsetMinutes must be between -720 and 840.");

        var nowUtc = DateTime.UtcNow;
        var nowLocal = nowUtc.AddMinutes(tz);
        var todayLocal = DateOnly.FromDateTime(nowLocal);

        var toDate = query.ToDate ?? todayLocal;
        var fromDate = query.FromDate ?? toDate.AddDays(-6);

        if (fromDate > toDate)
            throw new ArgumentException("fromDate must be <= toDate.");

        var daySpan = toDate.DayNumber - fromDate.DayNumber + 1;
        if (daySpan > 92)
            throw new ArgumentException("date range is too large (max 92 days).");

        var limit = query.Limit <= 0 ? 20 : query.Limit;
        if (limit > 200) limit = 200;

        var fromUtc = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, 0, 0, 0, DateTimeKind.Utc)
            .AddMinutes(-tz);

        var toUtcExclusive = new DateTime(toDate.Year, toDate.Month, toDate.Day, 0, 0, 0, DateTimeKind.Utc)
            .AddDays(1)
            .AddMinutes(-tz);

        return (fromDate, toDate, tz, limit, fromUtc, toUtcExclusive, todayLocal);
    }

    private async Task FillOrderSummaryAsync(
        ManagerDashboardOverviewResponse resp,
        List<int> franchiseIds,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        CancellationToken ct)
    {
        var data = await _db.StoreOrders
            .AsNoTracking()
            .Where(o => franchiseIds.Contains(o.FranchiseId))
            .Where(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtcExclusive)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        resp.OrderStatusSummary.Total = data.Sum(x => x.Count);
        resp.OrderStatusSummary.ByStatus = data
            .OrderByDescending(x => x.Count)
            .ToDictionary(x => x.Status ?? "UNKNOWN", x => x.Count, StringComparer.OrdinalIgnoreCase);
    }

    private async Task FillDeliverySummaryAsync(
        ManagerDashboardOverviewResponse resp,
        List<int> franchiseIds,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        CancellationToken ct)
    {
        var data = await _db.DeliveryPlans
            .AsNoTracking()
            .Where(p => franchiseIds.Contains(p.FranchiseId))
            .SelectMany(p => p.Deliveries)
            .Where(d => d.CreatedAt >= fromUtc && d.CreatedAt < toUtcExclusive)
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        resp.DeliveryStatusSummary.Total = data.Sum(x => x.Count);
        resp.DeliveryStatusSummary.ByStatus = data
            .OrderByDescending(x => x.Count)
            .ToDictionary(x => x.Status ?? "UNKNOWN", x => x.Count, StringComparer.OrdinalIgnoreCase);

        var deliveredStatuses = new[] { DeliveryStatus.Delivered, "COMPLETED" };
        var pendingStatuses = new[] { DeliveryStatus.Created, DeliveryStatus.Confirmed, "PLANNED", "IN_PROGRESS", "SHIPPING" };

        resp.DeliveryStatusSummary.DeliveredCount = data
            .Where(x => deliveredStatuses.Contains((x.Status ?? string.Empty).ToUpperInvariant()))
            .Sum(x => x.Count);

        resp.DeliveryStatusSummary.PendingCount = data
            .Where(x => pendingStatuses.Contains((x.Status ?? string.Empty).ToUpperInvariant()))
            .Sum(x => x.Count);
    }

    private async Task FillServiceLevelAsync(
        ManagerDashboardOverviewResponse resp,
        List<int> franchiseIds,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        int tzOffsetMinutes,
        CancellationToken ct)
    {
        var fromLocal = DateOnly.FromDateTime(fromUtc.AddMinutes(tzOffsetMinutes));
        var toLocal = DateOnly.FromDateTime(toUtcExclusive.AddMinutes(tzOffsetMinutes).AddDays(-1));

        var plannedQuery = _db.DeliveryPlans
            .AsNoTracking()
            .Where(p => franchiseIds.Contains(p.FranchiseId))
            .Where(p => p.PlannedDate >= fromLocal && p.PlannedDate <= toLocal);

        var totalPlannedDeliveries = await plannedQuery
            .SelectMany(p => p.Deliveries)
            .CountAsync(ct);

        if (totalPlannedDeliveries == 0)
        {
            resp.ServiceLevelSummary.TotalDeliveriesPlannedInRange = 0;
            resp.ServiceLevelSummary.TotalDeliveriesDeliveredInRange = 0;
            resp.ServiceLevelSummary.OnTimeDeliveredCount = 0;
            resp.ServiceLevelSummary.OnTimeRate = null;
            resp.Notes.Add("SERVICE_LEVEL: No delivery plans/deliveries in selected date range.");
            return;
        }

        var deliveries = await plannedQuery
            .SelectMany(p => p.Deliveries.Select(d => new
            {
                PlannedDate = p.PlannedDate,
                d.Status,
                d.DeliveredAt
            }))
            .ToListAsync(ct);

        static bool IsDelivered(string? status)
        {
            var s = (status ?? string.Empty).Trim().ToUpperInvariant();
            return s is DeliveryStatus.Delivered or "COMPLETED";
        }

        var delivered = deliveries.Where(x => IsDelivered(x.Status)).ToList();

        var onTimeCount = 0;
        foreach (var d in delivered)
        {
            var deliveredLocalDate = DateOnly.FromDateTime(d.DeliveredAt.AddMinutes(tzOffsetMinutes));
            if (deliveredLocalDate <= d.PlannedDate)
                onTimeCount++;
        }

        var deliveredCount = delivered.Count;

        resp.ServiceLevelSummary.TotalDeliveriesPlannedInRange = totalPlannedDeliveries;
        resp.ServiceLevelSummary.TotalDeliveriesDeliveredInRange = deliveredCount;
        resp.ServiceLevelSummary.OnTimeDeliveredCount = onTimeCount;
        resp.ServiceLevelSummary.OnTimeRate = totalPlannedDeliveries == 0
            ? null
            : Math.Round((decimal)onTimeCount / totalPlannedDeliveries, 4);
    }

    private async Task FillLowStockAlertsAsync(
        ManagerDashboardOverviewResponse resp,
        List<int> franchiseIds,
        int limit,
        CancellationToken ct)
    {
        var lowStocks = await _db.IngredientBatches
            .AsNoTracking()
            .Where(b =>
                b.Type == InventoryOwnerType.Franchise &&
                b.FranchiseId.HasValue &&
                franchiseIds.Contains(b.FranchiseId.Value))
            .Where(b => b.Ingredient.Status == IngredientStatus.Active)
            .GroupBy(b => new
            {
                FranchiseId = b.FranchiseId!.Value,
                b.IngredientId
            })
            .Select(g => new
            {
                g.Key.FranchiseId,
                g.Key.IngredientId,
                OnHand = g.Sum(x => x.Quantity)
            })
            .Join(
                _db.Ingredients.AsNoTracking().Where(i => i.Status == IngredientStatus.Active),
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
            .Where(x => x.OnHand < x.SafetyStock)
            .OrderBy(x => x.OnHand / (x.SafetyStock == 0 ? 1 : x.SafetyStock))
            .Take(limit)
            .ToListAsync(ct);

        if (lowStocks.Count == 0) return;

        var franchiseNames = await _db.Franchises
            .AsNoTracking()
            .Where(f => franchiseIds.Contains(f.FranchiseId))
            .ToDictionaryAsync(f => f.FranchiseId, f => f.Name, ct);

        resp.LowStockAlerts = lowStocks
            .Select(x => new LowStockAlertItem
            {
                FranchiseId = x.FranchiseId,
                FranchiseName = franchiseNames.TryGetValue(x.FranchiseId, out var name)
                    ? name
                    : $"Franchise #{x.FranchiseId}",
                IngredientId = x.IngredientId,
                IngredientName = x.Name,
                Unit = x.Unit,
                OnHandQuantity = x.OnHand,
                SafetyStock = x.SafetyStock
            })
            .ToList();
    }

    private async Task FillNearExpiryAlertsAsync(
        ManagerDashboardOverviewResponse resp,
        List<int> franchiseIds,
        int limit,
        DateOnly todayLocal,
        CancellationToken ct)
    {
        var nearExpiryDays = await GetNearExpiryDaysAsync(ct);
        if (nearExpiryDays <= 0)
        {
            resp.Notes.Add("NEAR_EXPIRY: System setting NEAR_EXPIRY_DAYS is not configured or invalid.");
            return;
        }

        var cutoff = todayLocal.AddDays(nearExpiryDays);

        var batches = await _db.IngredientBatches
            .AsNoTracking()
            .Where(b =>
                b.Type == InventoryOwnerType.Franchise &&
                b.FranchiseId.HasValue &&
                franchiseIds.Contains(b.FranchiseId.Value))
            .Where(b => b.Ingredient.Status == IngredientStatus.Active)
            .Where(b => b.ExpiredAt != null && b.ExpiredAt <= cutoff)
            .OrderBy(b => b.ExpiredAt)
            .ThenByDescending(b => b.Quantity)
            .Take(limit)
            .Select(b => new
            {
                FranchiseId = b.FranchiseId!.Value,
                b.IngredientId,
                IngredientName = b.Ingredient.Name,
                b.Ingredient.Unit,
                b.BatchId,
                b.BatchCode,
                b.Quantity,
                b.ExpiredAt
            })
            .ToListAsync(ct);

        if (batches.Count == 0) return;

        var franchiseNames = await _db.Franchises
            .AsNoTracking()
            .Where(f => franchiseIds.Contains(f.FranchiseId))
            .ToDictionaryAsync(f => f.FranchiseId, f => f.Name, ct);

        resp.NearExpiryAlerts = batches
            .Select(b =>
            {
                int? daysToExpire = null;
                if (b.ExpiredAt != null)
                    daysToExpire = b.ExpiredAt.Value.DayNumber - todayLocal.DayNumber;

                return new NearExpiryAlertItem
                {
                    FranchiseId = b.FranchiseId,
                    FranchiseName = franchiseNames.TryGetValue(b.FranchiseId, out var name)
                        ? name
                        : $"Franchise #{b.FranchiseId}",
                    IngredientId = b.IngredientId,
                    IngredientName = b.IngredientName,
                    Unit = b.Unit,
                    BatchId = b.BatchId,
                    BatchCode = b.BatchCode,
                    Quantity = b.Quantity,
                    ExpiredAt = b.ExpiredAt,
                    DaysToExpire = daysToExpire
                };
            })
            .ToList();
    }

    private async Task FillWasteAlertsAsync(
        ManagerDashboardOverviewResponse resp,
        List<int> franchiseIds,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        int limit,
        CancellationToken ct)
    {
        var movements = await _db.InventoryMovements
            .AsNoTracking()
            .Where(m => m.CreatedAt >= fromUtc && m.CreatedAt < toUtcExclusive)
            .Where(m =>
                m.Batch != null &&
                m.Batch.Type == InventoryOwnerType.Franchise &&
                m.Batch.FranchiseId.HasValue &&
                franchiseIds.Contains(m.Batch.FranchiseId.Value))
            .Where(m => m.Type == MovementType.Waste || m.Type == MovementType.Out)
            .GroupBy(m => new
            {
                FranchiseId = m.Batch!.FranchiseId!.Value,
                m.Batch.IngredientId
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
            resp.Notes.Add("WASTE: No inventory movements (OUT/WASTE) in selected date range.");
            return;
        }

        var ingredientMap = await _db.Ingredients
            .AsNoTracking()
            .Where(i => i.Status == IngredientStatus.Active)
            .Select(i => new
            {
                i.IngredientId,
                i.Name,
                i.Unit,
                i.WasteThreshold
            })
            .ToDictionaryAsync(i => i.IngredientId, i => i, ct);

        var franchiseNames = await _db.Franchises
            .AsNoTracking()
            .Where(f => franchiseIds.Contains(f.FranchiseId))
            .ToDictionaryAsync(f => f.FranchiseId, f => f.Name, ct);

        var alerts = new List<WasteAlertItem>();

        foreach (var x in movements)
        {
            if (!ingredientMap.TryGetValue(x.IngredientId, out var ing))
                continue;

            var denom = x.Waste + x.Out;
            decimal? rate = denom <= 0 ? null : Math.Round(x.Waste / denom, 6);

            var threshold = ing.WasteThreshold;
            var exceed = rate != null && threshold > 0 && rate.Value > threshold;

            alerts.Add(new WasteAlertItem
            {
                FranchiseId = x.FranchiseId,
                FranchiseName = franchiseNames.TryGetValue(x.FranchiseId, out var name)
                    ? name
                    : $"Franchise #{x.FranchiseId}",
                IngredientId = x.IngredientId,
                IngredientName = ing.Name,
                Unit = ing.Unit,
                WasteQuantity = x.Waste,
                IssuedQuantity = x.Out,
                WasteRate = rate,
                WasteThreshold = threshold,
                IsExceedThreshold = exceed
            });
        }

        resp.WasteAlerts = alerts
            .OrderByDescending(x => x.IsExceedThreshold)
            .ThenByDescending(x => x.WasteRate ?? 0)
            .ThenByDescending(x => x.WasteQuantity)
            .Take(limit)
            .ToList();
    }

    private async Task<int> GetNearExpiryDaysAsync(CancellationToken ct)
    {
        var raw = await _db.SystemSettings
            .AsNoTracking()
            .Where(s => s.Key == SettingKeys.NearExpiryDays)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(raw)) return 0;
        return int.TryParse(raw, out var x) ? x : 0;
    }
}