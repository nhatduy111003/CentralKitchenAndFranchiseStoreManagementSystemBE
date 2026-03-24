using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class SupplyDashboardService : ISupplyDashboardService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IDashboardScopeService _scopeService;

    public SupplyDashboardService(
        AppDbContext db,
        ICurrentUserService current,
        IDashboardScopeService scopeService)
    {
        _db = db;
        _current = current;
        _scopeService = scopeService;
    }

    /// <summary>Build the supply dashboard overview for shipment preparation and delivery follow-up.</summary>
    public async Task<SupplyDashboardOverviewResponse> GetOverviewAsync(SupplyDashboardOverviewQuery query, CancellationToken ct = default)
    {
        RequireAllowedRoles();
        query ??= new SupplyDashboardOverviewQuery();

        var scope = await _scopeService.ResolveCentralKitchenScopeAsync(query.CentralKitchenId, ct);
        var (fromDate, toDate, tz, limit) = NormalizeQuery(query);

        var managedFranchiseIds = await _scopeService.GetActiveFranchiseIdsByCentralKitchenAsync(scope.CentralKitchenId, ct);

        var response = new SupplyDashboardOverviewResponse
        {
            CentralKitchenId = scope.CentralKitchenId,
            CentralKitchenName = scope.CentralKitchenName,
            FromDate = fromDate,
            ToDate = toDate,
            TimezoneOffsetMinutes = tz,
            ManagedFranchiseCount = managedFranchiseIds.Count
        };

        await FillOrderStatusSummaryAsync(response, scope.CentralKitchenId, fromDate, toDate, ct);
        await FillDeliveryStatusSummaryAsync(response, scope.CentralKitchenId, fromDate, toDate, ct);
        await FillDroppedLineSummaryAsync(response, scope.CentralKitchenId, fromDate, toDate, ct);
        await FillReceivingSummaryAsync(response, scope.CentralKitchenId, fromDate, toDate, ct);
        await FillPriorityActionsAsync(response, scope.CentralKitchenId, fromDate, toDate, limit, ct);

        return response;
    }

    /// <summary>Enforce the roles that are allowed to access supply dashboard data.</summary>
    private void RequireAllowedRoles()
    {
        if (_current.IsInRole(RoleNames.Admin) ||
            _current.IsInRole(RoleNames.Manager) ||
            _current.IsInRole(RoleNames.SupplyCoordinator))
        {
            return;
        }

        throw new ForbiddenAccessException("You do not have permission to access supply dashboard.");
    }

    /// <summary>Normalize dashboard filters and clamp list limits.</summary>
    private static (DateOnly fromDate, DateOnly toDate, int timezoneOffsetMinutes, int limit) NormalizeQuery(SupplyDashboardOverviewQuery query)
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

        return (fromDate, toDate, tz, limit);
    }

    /// <summary>Aggregate supply-facing store-order statuses using business OrderDate.</summary>
    private async Task FillOrderStatusSummaryAsync(
        SupplyDashboardOverviewResponse response,
        int centralKitchenId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct)
    {
        var rows = await _db.StoreOrders
            .AsNoTracking()
            .Where(x => x.Franchise.CentralKitchenId == centralKitchenId)
            .Where(x => x.OrderDate >= fromDate && x.OrderDate <= toDate)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        response.OrderStatusSummary.Total = rows.Sum(x => x.Count);
        response.OrderStatusSummary.ByStatus = rows
            .OrderByDescending(x => x.Count)
            .ToDictionary(x => x.Status ?? "UNKNOWN", x => x.Count, StringComparer.OrdinalIgnoreCase);

        response.OrderStatusSummary.ForwardedToSupplyCount = GetStatusCount(rows, StoreOrderStatus.ForwardedToSupply);
        response.OrderStatusSummary.PreparingCount = GetStatusCount(rows, StoreOrderStatus.Preparing);
        response.OrderStatusSummary.ReadyToDeliverCount = GetStatusCount(rows, StoreOrderStatus.ReadyToDeliver);
        response.OrderStatusSummary.InTransitCount = GetStatusCount(rows, StoreOrderStatus.InTransit);
        response.OrderStatusSummary.DeliveredCount = GetStatusCount(rows, StoreOrderStatus.Delivered);
    }

    /// <summary>Aggregate delivery execution and receiving-confirmation metrics by planned date.</summary>
    private async Task FillDeliveryStatusSummaryAsync(
        SupplyDashboardOverviewResponse response,
        int centralKitchenId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct)
    {
        var rows = await _db.Deliveries
            .AsNoTracking()
            .Where(x => x.FromCentralKitchenId == centralKitchenId)
            .Where(x => x.DeliveryPlan.PlannedDate >= fromDate && x.DeliveryPlan.PlannedDate <= toDate)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        response.DeliveryStatusSummary.Total = rows.Sum(x => x.Count);
        response.DeliveryStatusSummary.ByStatus = rows
            .OrderByDescending(x => x.Count)
            .ToDictionary(x => x.Status ?? "UNKNOWN", x => x.Count, StringComparer.OrdinalIgnoreCase);

        response.DeliveryStatusSummary.DeliveredPendingReceivingCount = await _db.Deliveries
            .AsNoTracking()
            .Where(x => x.FromCentralKitchenId == centralKitchenId)
            .Where(x => x.DeliveryPlan.PlannedDate >= fromDate && x.DeliveryPlan.PlannedDate <= toDate)
            .Where(x => x.Status == DeliveryStatus.Delivered && !x.ReceivingReports.Any())
            .CountAsync(ct);

        response.DeliveryStatusSummary.ConfirmedReceivingCount = await _db.Deliveries
            .AsNoTracking()
            .Where(x => x.FromCentralKitchenId == centralKitchenId)
            .Where(x => x.DeliveryPlan.PlannedDate >= fromDate && x.DeliveryPlan.PlannedDate <= toDate)
            .Where(x => x.ReceivingReports.Any())
            .CountAsync(ct);
    }

    /// <summary>Summarize dropped delivery lines to expose fulfillment gaps early.</summary>
    private async Task FillDroppedLineSummaryAsync(
        SupplyDashboardOverviewResponse response,
        int centralKitchenId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct)
    {
        var droppedRows = await _db.DeliveryProductItems
            .AsNoTracking()
            .Where(x => x.Delivery.FromCentralKitchenId == centralKitchenId)
            .Where(x => x.Delivery.DeliveryPlan.PlannedDate >= fromDate && x.Delivery.DeliveryPlan.PlannedDate <= toDate)
            .Where(x => x.IsDropped)
            .Select(x => new
            {
                x.Delivery.DeliveryPlan.StoreOrderId,
                x.RequestedQuantity,
                x.Quantity
            })
            .ToListAsync(ct);

        response.DroppedLineSummary.DroppedLinesCount = droppedRows.Count;
        response.DroppedLineSummary.OrdersWithDroppedLinesCount = droppedRows
            .Where(x => x.StoreOrderId.HasValue)
            .Select(x => x.StoreOrderId!.Value)
            .Distinct()
            .Count();
        response.DroppedLineSummary.DroppedQuantity = droppedRows.Sum(x => Math.Max(x.RequestedQuantity - x.Quantity, 0m));
    }

    /// <summary>Expose current receiving-confirmation backlog from the supply perspective.</summary>
    private async Task FillReceivingSummaryAsync(
        SupplyDashboardOverviewResponse response,
        int centralKitchenId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct)
    {
        var deliveries = await _db.Deliveries
            .AsNoTracking()
            .Where(x => x.FromCentralKitchenId == centralKitchenId)
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

    /// <summary>Build a short actionable queue from supply-stage orders and pending confirmations.</summary>
    private async Task FillPriorityActionsAsync(
        SupplyDashboardOverviewResponse response,
        int centralKitchenId,
        DateOnly fromDate,
        DateOnly toDate,
        int limit,
        CancellationToken ct)
    {
        var priorityStatuses = new[]
        {
            StoreOrderStatus.ForwardedToSupply,
            StoreOrderStatus.Preparing,
            StoreOrderStatus.ReadyToDeliver,
            StoreOrderStatus.InTransit
        };

        var orders = await _db.StoreOrders
            .AsNoTracking()
            .Include(x => x.Franchise)
            .Where(x => x.Franchise.CentralKitchenId == centralKitchenId)
            .Where(x => x.OrderDate >= fromDate && x.OrderDate <= toDate)
            .Where(x => priorityStatuses.Contains(x.Status))
            .OrderBy(x => x.OrderDate)
            .ThenBy(x => x.ForwardedAt ?? x.PreparedAt ?? x.CreatedAt)
            .Take(limit)
            .Select(x => new
            {
                x.StoreOrderId,
                x.Status,
                x.OrderDate,
                OccurredAt = x.ForwardedAt ?? x.PreparedAt ?? x.CreatedAt,
                x.FranchiseId,
                FranchiseName = x.Franchise.Name
            })
            .ToListAsync(ct);

        response.PriorityActions = orders
            .Select(x => new SupplyActionItem
            {
                ActionType = x.Status,
                OrderId = x.StoreOrderId,
                OrderCode = BuildOrderCode(x.StoreOrderId),
                FranchiseId = x.FranchiseId,
                FranchiseName = x.FranchiseName,
                BusinessDate = x.OrderDate,
                OccurredAtUtc = x.OccurredAt,
                Message = x.Status switch
                {
                    StoreOrderStatus.ForwardedToSupply => $"Prepare forwarded order for {x.FranchiseName}.",
                    StoreOrderStatus.Preparing => $"Finish preparation for {x.FranchiseName}.",
                    StoreOrderStatus.ReadyToDeliver => $"Dispatch ready order to {x.FranchiseName}.",
                    StoreOrderStatus.InTransit => $"Follow up in-transit order to {x.FranchiseName}.",
                    _ => $"Review order {BuildOrderCode(x.StoreOrderId)}."
                }
            })
            .ToList();
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