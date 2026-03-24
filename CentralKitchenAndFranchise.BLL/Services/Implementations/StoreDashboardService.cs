using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Extensions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class StoreDashboardService : IStoreDashboardService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IDashboardScopeService _scopeService;

    public StoreDashboardService(
        AppDbContext db,
        ICurrentUserService current,
        IDashboardScopeService scopeService)
    {
        _db = db;
        _current = current;
        _scopeService = scopeService;
    }

    /// <summary>Build the franchise dashboard overview for store operations.</summary>
    public async Task<StoreDashboardOverviewResponse> GetOverviewAsync(StoreDashboardOverviewQuery query, CancellationToken ct = default)
    {
        RequireAllowedRoles();
        query ??= new StoreDashboardOverviewQuery();

        var scope = await _scopeService.ResolveFranchiseScopeAsync(query.FranchiseId, ct);
        var (fromDate, toDate, tz, limit, todayLocal) = NormalizeQuery(query);

        var response = new StoreDashboardOverviewResponse
        {
            FranchiseId = scope.FranchiseId,
            FranchiseName = scope.FranchiseName,
            CentralKitchenId = scope.CentralKitchenId,
            CentralKitchenName = scope.CentralKitchenName,
            FromDate = fromDate,
            ToDate = toDate,
            TimezoneOffsetMinutes = tz
        };

        await FillOrderSummaryAsync(response, scope.FranchiseId, fromDate, toDate, ct);
        await FillReceivingSummaryAsync(response, scope.FranchiseId, fromDate, toDate, ct);
        await FillInventorySummaryAsync(response, scope.FranchiseId, todayLocal, ct);
        await FillLowStockAlertsAsync(response, scope.FranchiseId, limit, ct);
        await FillNearExpiryAlertsAsync(response, scope.FranchiseId, limit, todayLocal, ct);
        await FillRecentDeliveriesAsync(response, scope.FranchiseId, fromDate, toDate, limit, ct);

        return response;
    }

    /// <summary>Enforce the roles that are allowed to access store dashboard data.</summary>
    private void RequireAllowedRoles()
    {
        if (_current.IsInRole(RoleNames.Admin) ||
            _current.IsInRole(RoleNames.Manager) ||
            _current.IsInRole(RoleNames.StoreStaff))
        {
            return;
        }

        throw new ForbiddenAccessException("You do not have permission to access store dashboard.");
    }

    /// <summary>Normalize dashboard filters and clamp list limits.</summary>
    private static (DateOnly fromDate, DateOnly toDate, int timezoneOffsetMinutes, int limit, DateOnly todayLocal) NormalizeQuery(StoreDashboardOverviewQuery query)
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

    /// <summary>Aggregate franchise order metrics using business OrderDate.</summary>
    private async Task FillOrderSummaryAsync(
        StoreDashboardOverviewResponse response,
        int franchiseId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct)
    {
        var rows = await _db.StoreOrders
            .AsNoTracking()
            .Where(x => x.FranchiseId == franchiseId)
            .Where(x => x.OrderDate >= fromDate && x.OrderDate <= toDate)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        response.OrderSummary.Total = rows.Sum(x => x.Count);
        response.OrderSummary.ByStatus = rows
            .OrderByDescending(x => x.Count)
            .ToDictionary(x => x.Status ?? "UNKNOWN", x => x.Count, StringComparer.OrdinalIgnoreCase);

        response.OrderSummary.ActiveOrderCount = rows
            .Where(x =>
                !string.Equals(x.Status, StoreOrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(x.Status, StoreOrderStatus.ReceivedByStore, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Count);

        response.OrderSummary.DeliveredPendingReceivingCount = rows
            .Where(x => string.Equals(x.Status, StoreOrderStatus.Delivered, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Count);

        response.OrderSummary.ReceivedCount = rows
            .Where(x => string.Equals(x.Status, StoreOrderStatus.ReceivedByStore, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Count);
    }

    /// <summary>Aggregate receiving-confirmation status from delivery and receiving records.</summary>
    private async Task FillReceivingSummaryAsync(
        StoreDashboardOverviewResponse response,
        int franchiseId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct)
    {
        var deliveries = await _db.Deliveries
            .AsNoTracking()
            .Where(x => x.DeliveryPlan.FranchiseId == franchiseId)
            .Where(x => x.DeliveryPlan.PlannedDate >= fromDate && x.DeliveryPlan.PlannedDate <= toDate)
            .Select(x => new
            {
                x.Status,
                x.DeliveredAt,
                x.ConfirmedAt,
                HasReceiving = x.ReceivingReports.Any()
            })
            .ToListAsync(ct);

        response.ReceivingSummary.PendingConfirmationCount = deliveries.Count(x =>
            string.Equals(x.Status, DeliveryStatus.Delivered, StringComparison.OrdinalIgnoreCase) &&
            !x.HasReceiving);

        response.ReceivingSummary.ConfirmedCount = deliveries.Count(x => x.HasReceiving);

        response.ReceivingSummary.LatestDeliveredAtUtc = deliveries
            .Where(x => x.DeliveredAt.HasValue)
            .OrderByDescending(x => x.DeliveredAt)
            .Select(x => x.DeliveredAt)
            .FirstOrDefault();

        response.ReceivingSummary.LatestConfirmedAtUtc = deliveries
            .Where(x => x.ConfirmedAt.HasValue)
            .OrderByDescending(x => x.ConfirmedAt)
            .Select(x => x.ConfirmedAt)
            .FirstOrDefault();
    }

    /// <summary>Build an at-a-glance inventory-health snapshot from current on-hand stock.</summary>
    private async Task FillInventorySummaryAsync(
        StoreDashboardOverviewResponse response,
        int franchiseId,
        DateOnly todayLocal,
        CancellationToken ct)
    {
        var ingredientBatches = await _db.IngredientBatches
            .AsNoTracking()
            .Include(x => x.Ingredient)
            .Where(x =>
                x.Type == InventoryOwnerType.Franchise &&
                x.FranchiseId == franchiseId &&
                x.Quantity > 0)
            .ToListAsync(ct);

        var productBatches = await _db.ProductBatches
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.FranchiseId == franchiseId && x.Quantity > 0)
            .ToListAsync(ct);

        response.InventorySummary.IngredientItemCount = ingredientBatches
            .Select(x => x.IngredientId)
            .Distinct()
            .Count();

        response.InventorySummary.ProductItemCount = productBatches
            .Select(x => x.ProductId)
            .Distinct()
            .Count();

        response.InventorySummary.TotalIngredientOnHand = ingredientBatches.Sum(x => x.Quantity);
        response.InventorySummary.TotalProductOnHand = productBatches.Sum(x => x.Quantity);

        response.InventorySummary.LowStockIngredientCount = ingredientBatches
            .GroupBy(x => new { x.IngredientId, x.Ingredient.SafetyStock })
            .Count(g => g.Key.SafetyStock > 0 && g.Sum(x => x.Quantity) < g.Key.SafetyStock);

        var nearExpiryDays = await GetNearExpiryDaysAsync(ct);
        if (nearExpiryDays <= 0)
        {
            response.Notes.Add("NEAR_EXPIRY: System setting NEAR_EXPIRY_DAYS is not configured or invalid.");
            return;
        }

        var cutoff = todayLocal.AddDays(nearExpiryDays);

        response.InventorySummary.NearExpiryIngredientBatchCount = ingredientBatches
            .Count(x =>
            {
                var expiredAt = x.CalculateExpiredAt();
                return expiredAt != null && expiredAt <= cutoff;
            });
    }

    /// <summary>Return the most critical low-stock ingredients for the franchise.</summary>
    private async Task FillLowStockAlertsAsync(
        StoreDashboardOverviewResponse response,
        int franchiseId,
        int limit,
        CancellationToken ct)
    {
        var rows = await _db.IngredientBatches
            .AsNoTracking()
            .Where(x =>
                x.Type == InventoryOwnerType.Franchise &&
                x.FranchiseId == franchiseId)
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

        response.LowStockAlerts = rows
            .Select(x => new StoreLowStockAlertItem
            {
                IngredientId = x.IngredientId,
                IngredientName = x.Name,
                Unit = x.Unit,
                OnHandQuantity = x.OnHand,
                SafetyStock = x.SafetyStock
            })
            .ToList();
    }

    /// <summary>Return the most urgent near-expiry ingredient batches for the franchise.</summary>
    private async Task FillNearExpiryAlertsAsync(
        StoreDashboardOverviewResponse response,
        int franchiseId,
        int limit,
        DateOnly todayLocal,
        CancellationToken ct)
    {
        var nearExpiryDays = await GetNearExpiryDaysAsync(ct);
        if (nearExpiryDays <= 0)
            return;

        var cutoff = todayLocal.AddDays(nearExpiryDays);

        var batches = await _db.IngredientBatches
            .AsNoTracking()
            .Include(x => x.Ingredient)
            .Where(x =>
                x.Type == InventoryOwnerType.Franchise &&
                x.FranchiseId == franchiseId &&
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
            .Select(x => new StoreNearExpiryAlertItem
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

    /// <summary>Return a recent delivery list so the store can track last-mile status quickly.</summary>
    private async Task FillRecentDeliveriesAsync(
        StoreDashboardOverviewResponse response,
        int franchiseId,
        DateOnly fromDate,
        DateOnly toDate,
        int limit,
        CancellationToken ct)
    {
        response.RecentDeliveries = await _db.Deliveries
            .AsNoTracking()
            .Where(x => x.DeliveryPlan.FranchiseId == franchiseId)
            .Where(x => x.DeliveryPlan.PlannedDate >= fromDate && x.DeliveryPlan.PlannedDate <= toDate)
            .OrderByDescending(x => x.DeliveredAt ?? x.CreatedAt)
            .ThenByDescending(x => x.DeliveryId)
            .Take(limit)
            .Select(x => new StoreRecentDeliveryItem
            {
                DeliveryId = x.DeliveryId,
                DeliveryCode = $"DLV-{x.DeliveryId:D6}",
                PlannedDate = x.DeliveryPlan.PlannedDate,
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                DeliveredAt = x.DeliveredAt,
                ConfirmedAt = x.ConfirmedAt,
                TotalItems = x.ProductItems.Count() + x.IngredientItems.Count(),
                TotalQuantity = x.ProductItems.Sum(p => (decimal?)p.Quantity) ?? 0m
                    + (x.IngredientItems.Sum(i => (decimal?)i.Quantity) ?? 0m)
            })
            .ToListAsync(ct);
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
}