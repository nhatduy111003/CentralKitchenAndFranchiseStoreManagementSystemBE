using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Extensions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.Enums;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class KitchenDashboardService : IKitchenDashboardService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IDashboardScopeService _scopeService;

    public KitchenDashboardService(
        AppDbContext db,
        ICurrentUserService current,
        IDashboardScopeService scopeService)
    {
        _db = db;
        _current = current;
        _scopeService = scopeService;
    }

    /// <summary>Build the central-kitchen dashboard overview for kitchen operations.</summary>
    public async Task<KitchenDashboardOverviewResponse> GetOverviewAsync(KitchenDashboardOverviewQuery query, CancellationToken ct = default)
    {
        RequireAllowedRoles();
        query ??= new KitchenDashboardOverviewQuery();

        var scope = await _scopeService.ResolveCentralKitchenScopeAsync(query.CentralKitchenId, ct);
        var (fromDate, toDate, tz, limit, todayLocal) = NormalizeQuery(query);

        var managedFranchiseIds = await _scopeService.GetActiveFranchiseIdsByCentralKitchenAsync(scope.CentralKitchenId, ct);

        var response = new KitchenDashboardOverviewResponse
        {
            CentralKitchenId = scope.CentralKitchenId,
            CentralKitchenName = scope.CentralKitchenName,
            FromDate = fromDate,
            ToDate = toDate,
            TimezoneOffsetMinutes = tz,
            ManagedFranchiseCount = managedFranchiseIds.Count
        };

        await FillOrderQueueSummaryAsync(response, scope.CentralKitchenId, fromDate, toDate, todayLocal, ct);
        await FillProductionPlanSummaryAsync(response, scope.CentralKitchenId, fromDate, toDate, todayLocal, ct);
        await FillProductionRunSummaryAsync(response, scope.CentralKitchenId, fromDate, toDate, ct);
        await FillLowStockAlertsAsync(response, scope.CentralKitchenId, limit, ct);
        await FillNearExpiryAlertsAsync(response, scope.CentralKitchenId, limit, todayLocal, ct);
        await FillPriorityActionsAsync(response, scope.CentralKitchenId, fromDate, toDate, limit, ct);

        return response;
    }

    /// <summary>Enforce the roles that are allowed to access kitchen dashboard data.</summary>
    private void RequireAllowedRoles()
    {
        if (_current.IsInRole(RoleNames.Admin) ||
            _current.IsInRole(RoleNames.Manager) ||
            _current.IsInRole(RoleNames.KitchenStaff))
        {
            return;
        }

        throw new ForbiddenAccessException("You do not have permission to access kitchen dashboard.");
    }

    /// <summary>Normalize dashboard filters and protect the database from oversized queries.</summary>
    private static (DateOnly fromDate, DateOnly toDate, int timezoneOffsetMinutes, int limit, DateOnly todayLocal) NormalizeQuery(KitchenDashboardOverviewQuery query)
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

        var limit = query.Limit <= 0 ? 10 : query.Limit;
        if (limit > 100) limit = 100;

        return (fromDate, toDate, tz, limit, todayLocal);
    }

    /// <summary>Aggregate kitchen-facing order queue metrics using business OrderDate.</summary>
    private async Task FillOrderQueueSummaryAsync(
        KitchenDashboardOverviewResponse response,
        int centralKitchenId,
        DateOnly fromDate,
        DateOnly toDate,
        DateOnly todayLocal,
        CancellationToken ct)
    {
        var rows = await _db.StoreOrders
            .AsNoTracking()
            .Where(x => x.Franchise.CentralKitchenId == centralKitchenId)
            .Where(x => x.OrderDate >= fromDate && x.OrderDate <= toDate)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        response.OrderQueueSummary.Total = rows.Sum(x => x.Count);
        response.OrderQueueSummary.ByStatus = rows
            .OrderByDescending(x => x.Count)
            .ToDictionary(x => x.Status ?? "UNKNOWN", x => x.Count, StringComparer.OrdinalIgnoreCase);

        response.OrderQueueSummary.LockedCount = GetStatusCount(rows, StoreOrderStatus.Locked);
        response.OrderQueueSummary.ReceivedByKitchenCount = GetStatusCount(rows, StoreOrderStatus.ReceivedByKitchen);
        response.OrderQueueSummary.ForwardedToSupplyCount = GetStatusCount(rows, StoreOrderStatus.ForwardedToSupply);

        response.OrderQueueSummary.OverdueActionCount = await _db.StoreOrders
            .AsNoTracking()
            .Where(x => x.Franchise.CentralKitchenId == centralKitchenId)
            .Where(x => x.OrderDate < todayLocal)
            .Where(x =>
                x.Status == StoreOrderStatus.Locked ||
                x.Status == StoreOrderStatus.ReceivedByKitchen)
            .CountAsync(ct);
    }

    /// <summary>Aggregate production-plan metrics using PlanDate as the operational date.</summary>
    private async Task FillProductionPlanSummaryAsync(
        KitchenDashboardOverviewResponse response,
        int centralKitchenId,
        DateOnly fromDate,
        DateOnly toDate,
        DateOnly todayLocal,
        CancellationToken ct)
    {
        var plans = await _db.ProductionPlans
            .AsNoTracking()
            .Where(x => x.CentralKitchenId == centralKitchenId)
            .Where(x => x.PlanDate >= fromDate && x.PlanDate <= toDate)
            .Select(x => new
            {
                x.ProductionPlanId,
                x.PlanDate,
                x.Status
            })
            .ToListAsync(ct);

        var grouped = plans
            .GroupBy(x => x.Status?.ToString() ?? "UNKNOWN")
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToList();

        response.ProductionPlanSummary.Total = grouped.Sum(x => x.Count);
        response.ProductionPlanSummary.ByStatus = grouped
            .OrderByDescending(x => x.Count)
            .ToDictionary(x => x.Status, x => x.Count, StringComparer.OrdinalIgnoreCase);

        response.ProductionPlanSummary.DueTodayOpenCount = plans.Count(x =>
            x.PlanDate == todayLocal &&
            x.Status is not ProductionPlanStatus.COMPLETED and not ProductionPlanStatus.CANCELLED);

        response.ProductionPlanSummary.OverdueOpenCount = plans.Count(x =>
            x.PlanDate < todayLocal &&
            x.Status is not ProductionPlanStatus.COMPLETED and not ProductionPlanStatus.CANCELLED);

        response.ProductionPlanSummary.TotalPlannedQuantity = await _db.ProductionPlanItems
            .AsNoTracking()
            .Where(x =>
                x.ProductionPlan.CentralKitchenId == centralKitchenId &&
                x.ProductionPlan.PlanDate >= fromDate &&
                x.ProductionPlan.PlanDate <= toDate)
            .SumAsync(x => (decimal?)x.Quantity, ct) ?? 0m;
    }

    /// <summary>Aggregate production-run execution metrics for the selected date range.</summary>
    private async Task FillProductionRunSummaryAsync(
        KitchenDashboardOverviewResponse response,
        int centralKitchenId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct)
    {
        var rows = await _db.ProductionRuns
            .AsNoTracking()
            .Where(x => x.CentralKitchenId == centralKitchenId)
            .Where(x => x.ProductionDate >= fromDate && x.ProductionDate <= toDate)
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count(),
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToListAsync(ct);

        response.ProductionRunSummary.Total = rows.Sum(x => x.Count);
        response.ProductionRunSummary.ByStatus = rows
            .OrderByDescending(x => x.Count)
            .ToDictionary(x => x.Status ?? "UNKNOWN", x => x.Count, StringComparer.OrdinalIgnoreCase);

        response.ProductionRunSummary.TotalRunQuantity = rows.Sum(x => x.Quantity);
        response.ProductionRunSummary.CompletedQuantity = rows
            .Where(x => string.Equals(x.Status, ProductionRunStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Quantity);
    }

    /// <summary>Return the most critical central-kitchen ingredient low-stock alerts.</summary>
    private async Task FillLowStockAlertsAsync(
        KitchenDashboardOverviewResponse response,
        int centralKitchenId,
        int limit,
        CancellationToken ct)
    {
        var lowStockRows = await _db.IngredientBatches
            .AsNoTracking()
            .Where(x =>
                x.Type == InventoryOwnerType.CentralKitchen &&
                x.CentralKitchenId == centralKitchenId &&
                x.FranchiseId == null)
            .Where(x => x.Ingredient.Status == IngredientStatus.Active)
            .GroupBy(x => new
            {
                x.IngredientId,
                x.Ingredient.Name,
                x.Ingredient.Unit,
                x.Ingredient.SafetyStock
            })
            .Select(g => new
            {
                g.Key.IngredientId,
                g.Key.Name,
                g.Key.Unit,
                g.Key.SafetyStock,
                OnHand = g.Sum(x => x.Quantity)
            })
            .Where(x => x.SafetyStock > 0 && x.OnHand < x.SafetyStock)
            .OrderBy(x => x.OnHand / x.SafetyStock)
            .ThenBy(x => x.Name)
            .Take(limit)
            .ToListAsync(ct);

        response.LowStockAlerts = lowStockRows
            .Select(x => new KitchenLowStockAlertItem
            {
                IngredientId = x.IngredientId,
                IngredientName = x.Name,
                Unit = x.Unit,
                OnHandQuantity = x.OnHand,
                SafetyStock = x.SafetyStock
            })
            .ToList();
    }

    /// <summary>Return the most urgent central-kitchen ingredient near-expiry batches.</summary>
    private async Task FillNearExpiryAlertsAsync(
        KitchenDashboardOverviewResponse response,
        int centralKitchenId,
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
                x.Type == InventoryOwnerType.CentralKitchen &&
                x.CentralKitchenId == centralKitchenId &&
                x.FranchiseId == null &&
                x.Quantity > 0)
            .ToListAsync(ct);

        response.NearExpiryAlerts = batches
            .Select(x => new
            {
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
            .Select(x => new KitchenNearExpiryAlertItem
            {
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

    /// <summary>Build a short action list for kitchen staff from overdue and newly locked orders.</summary>
    private async Task FillPriorityActionsAsync(
        KitchenDashboardOverviewResponse response,
        int centralKitchenId,
        DateOnly fromDate,
        DateOnly toDate,
        int limit,
        CancellationToken ct)
    {
        var actionStatuses = new[] { StoreOrderStatus.Locked, StoreOrderStatus.ReceivedByKitchen };

        var orders = await _db.StoreOrders
            .AsNoTracking()
            .Include(x => x.Franchise)
            .Where(x => x.Franchise.CentralKitchenId == centralKitchenId)
            .Where(x => x.OrderDate >= fromDate && x.OrderDate <= toDate)
            .Where(x => actionStatuses.Contains(x.Status))
            .OrderBy(x => x.OrderDate)
            .ThenBy(x => x.CreatedAt)
            .Take(limit)
            .Select(x => new
            {
                x.StoreOrderId,
                x.Status,
                x.OrderDate,
                x.CreatedAt,
                FranchiseName = x.Franchise.Name
            })
            .ToListAsync(ct);

        response.PriorityActions = orders
            .Select(x => new KitchenActionItem
            {
                ActionType = x.Status,
                RelatedId = x.StoreOrderId,
                RelatedCode = BuildOrderCode(x.StoreOrderId),
                BusinessDate = x.OrderDate,
                OccurredAtUtc = x.CreatedAt,
                Message = x.Status == StoreOrderStatus.Locked
                    ? $"Receive locked order from {x.FranchiseName}."
                    : $"Review and forward received order from {x.FranchiseName}."
            })
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

    /// <summary>Return a status count from a grouped result set with case-insensitive matching.</summary>
    private static int GetStatusCount<T>(IEnumerable<T> rows, string status) where T : class
    {
        var statusProperty = typeof(T).GetProperty("Status");
        var countProperty = typeof(T).GetProperty("Count");

        foreach (var row in rows)
        {
            var rowStatus = statusProperty?.GetValue(row) as string;
            if (string.Equals(rowStatus, status, StringComparison.OrdinalIgnoreCase))
                return (int)(countProperty?.GetValue(row) ?? 0);
        }

        return 0;
    }

    /// <summary>Format a user-facing store-order code from the numeric id.</summary>
    private static string BuildOrderCode(int orderId) => $"SO-{orderId:D6}";
}