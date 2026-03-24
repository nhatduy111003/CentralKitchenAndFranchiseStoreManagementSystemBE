using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.Enums;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public AdminDashboardService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    /// <summary>Build the admin dashboard with system-wide operational and governance metrics.</summary>
    public async Task<AdminDashboardOverviewResponse> GetOverviewAsync(AdminDashboardOverviewQuery query, CancellationToken ct = default)
    {
        RequireAdmin();
        query ??= new AdminDashboardOverviewQuery();

        var (fromDate, toDate, tzOffsetMinutes, top, fromUtc, toUtcExclusive) = NormalizeQuery(query);

        var response = new AdminDashboardOverviewResponse
        {
            FromDate = fromDate,
            ToDate = toDate,
            TimezoneOffsetMinutes = tzOffsetMinutes
        };

        await FillCentralKitchenSummaryAsync(response, ct);
        await FillFranchiseSummaryAsync(response, ct);
        await FillUserSummaryAsync(response, ct);
        await FillRbacSummaryAsync(response, ct);
        await FillOperationalSnapshotAsync(response, ct);

        await FillAuditActivityAsync(response, fromUtc, toUtcExclusive, top, ct);
        await FillStoreOrderWorkloadAsync(response, fromDate, toDate, top, ct);
        await FillDeliveryWorkloadAsync(response, fromDate, toDate, top, ct);
        await FillProductionPlanStatusWorkloadAsync(response, fromDate, toDate, top, ct);
        await FillSupportRequestWorkloadAsync(response, fromUtc, toUtcExclusive, top, ct);

        await FillDataFreshnessAsync(response, ct);

        return response;
    }

    /// <summary>Enforce the Admin-only permission for this dashboard.</summary>
    private void RequireAdmin()
    {
        if (!_current.IsInRole(RoleNames.Admin))
            throw new ForbiddenAccessException("Admin role required.");
    }

    /// <summary>Normalize dashboard filters and derive the UTC window for CreatedAt-based metrics.</summary>
    private static (DateOnly fromDate, DateOnly toDate, int tzOffsetMinutes, int top, DateTime fromUtc, DateTime toUtcExclusive) NormalizeQuery(AdminDashboardOverviewQuery query)
    {
        var tzOffsetMinutes = query.TimezoneOffsetMinutes ?? 0;
        if (tzOffsetMinutes is < -14 * 60 or > 14 * 60)
            throw new ArgumentException("timezoneOffsetMinutes must be between -840 and 840.");

        var todayLocal = DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(tzOffsetMinutes));
        var toDate = query.ToDate ?? todayLocal;
        var fromDate = query.FromDate ?? toDate.AddDays(-6);

        if (fromDate > toDate)
            throw new ArgumentException("fromDate must be <= toDate.");

        if (toDate.DayNumber - fromDate.DayNumber > 366)
            throw new ArgumentException("date range too large (max 366 days).");

        var top = query.Top <= 0 ? 10 : query.Top;
        if (top > 50) top = 50;

        var fromLocal = fromDate.ToDateTime(TimeOnly.MinValue);
        var toLocalExclusive = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var fromUtc = DateTime.SpecifyKind(fromLocal.AddMinutes(-tzOffsetMinutes), DateTimeKind.Utc);
        var toUtcExclusive = DateTime.SpecifyKind(toLocalExclusive.AddMinutes(-tzOffsetMinutes), DateTimeKind.Utc);

        return (fromDate, toDate, tzOffsetMinutes, top, fromUtc, toUtcExclusive);
    }

    /// <summary>Aggregate total and active central-kitchen counts.</summary>
    private async Task FillCentralKitchenSummaryAsync(AdminDashboardOverviewResponse response, CancellationToken ct)
    {
        var grouped = await _db.CentralKitchens
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        response.CentralKitchenSummary.Total = grouped.Sum(x => x.Count);
        response.CentralKitchenSummary.Active = GetStatusCount(grouped, OrganizationStatus.Active);
        response.CentralKitchenSummary.Inactive = GetStatusCount(grouped, OrganizationStatus.Inactive);
    }

    /// <summary>Aggregate total and active franchise counts.</summary>
    private async Task FillFranchiseSummaryAsync(AdminDashboardOverviewResponse response, CancellationToken ct)
    {
        var grouped = await _db.Franchises
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        response.FranchiseSummary.Total = grouped.Sum(x => x.Count);
        response.FranchiseSummary.Active = GetStatusCount(grouped, OrganizationStatus.Active);
        response.FranchiseSummary.Inactive = GetStatusCount(grouped, OrganizationStatus.Inactive);
    }

    /// <summary>Aggregate user counts and active users by role.</summary>
    private async Task FillUserSummaryAsync(AdminDashboardOverviewResponse response, CancellationToken ct)
    {
        var grouped = await _db.Users
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        response.UserSummary.Total = grouped.Sum(x => x.Count);
        response.UserSummary.Active = GetStatusCount(grouped, OrganizationStatus.Active);
        response.UserSummary.Inactive = GetStatusCount(grouped, OrganizationStatus.Inactive);

        var activeByRole = await _db.Users
            .AsNoTracking()
            .Where(x => x.Status == OrganizationStatus.Active)
            .GroupBy(x => x.Role.Name)
            .Select(g => new { RoleName = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        response.UserSummary.ActiveUsersByRole = activeByRole.ToDictionary(
            x => x.RoleName ?? "UNKNOWN",
            x => x.Count,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Aggregate role, permission, and role-permission link counts.</summary>
    private async Task FillRbacSummaryAsync(AdminDashboardOverviewResponse response, CancellationToken ct)
    {
        var roleGrouped = await _db.Roles
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        response.RbacSummary.RoleActiveCount = GetStatusCount(roleGrouped, OrganizationStatus.Active);
        response.RbacSummary.RoleInactiveCount = GetStatusCount(roleGrouped, OrganizationStatus.Inactive);

        var permissionGrouped = await _db.Permissions
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        response.RbacSummary.PermissionActiveCount = GetStatusCount(permissionGrouped, OrganizationStatus.Active);
        response.RbacSummary.PermissionInactiveCount = GetStatusCount(permissionGrouped, OrganizationStatus.Inactive);
        response.RbacSummary.RolePermissionLinkCount = await _db.RolePermissions.AsNoTracking().CountAsync(ct);
    }

    /// <summary>Compute current open-workflow counts that are useful for system-level monitoring.</summary>
    private async Task FillOperationalSnapshotAsync(AdminDashboardOverviewResponse response, CancellationToken ct)
    {
        response.OperationalSnapshot.OpenStoreOrdersCount = await _db.StoreOrders
            .AsNoTracking()
            .Where(x =>
                x.Status != StoreOrderStatus.Cancelled &&
                x.Status != StoreOrderStatus.ReceivedByStore)
            .CountAsync(ct);

        response.OperationalSnapshot.ActiveProductionPlansCount = await _db.ProductionPlans
            .AsNoTracking()
            .Where(x => x.Status != ProductionPlanStatus.COMPLETED && x.Status != ProductionPlanStatus.CANCELLED)
            .CountAsync(ct);

        response.OperationalSnapshot.OpenDeliveriesCount = await _db.Deliveries
            .AsNoTracking()
            .Where(x =>
                x.Status != DeliveryStatus.Confirmed &&
                x.Status != DeliveryStatus.Cancelled)
            .CountAsync(ct);

        response.OperationalSnapshot.PendingReceivingCount = await _db.Deliveries
            .AsNoTracking()
            .Where(x => x.Status == DeliveryStatus.Delivered && !x.ReceivingReports.Any())
            .CountAsync(ct);
    }

    /// <summary>Aggregate audit activity within the selected CreatedAt UTC window.</summary>
    private async Task FillAuditActivityAsync(AdminDashboardOverviewResponse response, DateTime fromUtc, DateTime toUtcExclusive, int top, CancellationToken ct)
    {
        var baseQuery = _db.AuditLogs
            .AsNoTracking()
            .Where(x => x.CreatedAt >= fromUtc && x.CreatedAt < toUtcExclusive);

        response.AuditActivity.TotalInRange = await baseQuery.CountAsync(ct);

        response.AuditActivity.MostRecentAuditAtUtc = await baseQuery
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        response.AuditActivity.TopActions = await baseQuery
            .GroupBy(x => x.Action)
            .Select(g => new NamedCount
            {
                Name = g.Key ?? "UNKNOWN",
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .Take(top)
            .ToListAsync(ct);

        response.AuditActivity.TopEntities = await baseQuery
            .GroupBy(x => string.IsNullOrWhiteSpace(x.EntityName) ? "UNKNOWN" : x.EntityName!)
            .Select(g => new NamedCount
            {
                Name = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .Take(top)
            .ToListAsync(ct);
    }

    /// <summary>Aggregate store-order workload using OrderDate as the business date.</summary>
    private async Task FillStoreOrderWorkloadAsync(AdminDashboardOverviewResponse response, DateOnly fromDate, DateOnly toDate, int top, CancellationToken ct)
    {
        var rows = await _db.StoreOrders
            .AsNoTracking()
            .Where(x => x.OrderDate >= fromDate && x.OrderDate <= toDate)
            .GroupBy(x => x.Status)
            .Select(g => new NamedCount
            {
                Name = g.Key ?? "UNKNOWN",
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .Take(top)
            .ToListAsync(ct);

        response.StoreOrders.TotalInRange = rows.Sum(x => x.Count);
        response.StoreOrders.TopStatuses = rows;
    }

    /// <summary>Aggregate delivery workload using DeliveryPlan.PlannedDate as the business date.</summary>
    private async Task FillDeliveryWorkloadAsync(AdminDashboardOverviewResponse response, DateOnly fromDate, DateOnly toDate, int top, CancellationToken ct)
    {
        var rows = await _db.Deliveries
            .AsNoTracking()
            .Where(x => x.DeliveryPlan.PlannedDate >= fromDate && x.DeliveryPlan.PlannedDate <= toDate)
            .GroupBy(x => x.Status)
            .Select(g => new NamedCount
            {
                Name = g.Key ?? "UNKNOWN",
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .Take(top)
            .ToListAsync(ct);

        response.Deliveries.TotalInRange = rows.Sum(x => x.Count);
        response.Deliveries.TopStatuses = rows;
    }

    /// <summary>Aggregate production-plan workload using PlanDate as the business date.</summary>
    private async Task FillProductionPlanStatusWorkloadAsync(AdminDashboardOverviewResponse response, DateOnly fromDate, DateOnly toDate, int top, CancellationToken ct)
    {
        var rows = await _db.ProductionPlans
            .AsNoTracking()
            .Where(x => x.PlanDate >= fromDate && x.PlanDate <= toDate)
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(top)
            .ToListAsync(ct);

        response.ProductionPlans.TotalInRange = rows.Sum(x => x.Count);
        response.ProductionPlans.TopStatuses = rows
            .Select(x => new NamedCount
            {
                Name = x.Status?.ToString() ?? "UNKNOWN",
                Count = x.Count
            })
            .ToList();
    }

    /// <summary>Aggregate support-request workload using CreatedAt because no business date exists.</summary>
    private async Task FillSupportRequestWorkloadAsync(AdminDashboardOverviewResponse response, DateTime fromUtc, DateTime toUtcExclusive, int top, CancellationToken ct)
    {
        var rows = await _db.SupportRequests
            .AsNoTracking()
            .Where(x => x.CreatedAt >= fromUtc && x.CreatedAt < toUtcExclusive)
            .GroupBy(x => x.Status)
            .Select(g => new NamedCount
            {
                Name = g.Key ?? "UNKNOWN",
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .Take(top)
            .ToListAsync(ct);

        response.SupportRequests.TotalInRange = rows.Sum(x => x.Count);
        response.SupportRequests.TopStatuses = rows;
    }

    /// <summary>Read latest timestamps for quick data-freshness validation.</summary>
    private async Task FillDataFreshnessAsync(AdminDashboardOverviewResponse response, CancellationToken ct)
    {
        response.DataFreshness.LatestAuditLogAtUtc = await _db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        response.DataFreshness.LatestUserUpdatedAtUtc = await _db.Users
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => (DateTime?)x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        response.DataFreshness.LatestFranchiseUpdatedAtUtc = await _db.Franchises
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => (DateTime?)x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        response.DataFreshness.LatestCentralKitchenUpdatedAtUtc = await _db.CentralKitchens
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => (DateTime?)x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        response.DataFreshness.LatestStoreOrderAtUtc = await _db.StoreOrders
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        response.DataFreshness.LatestDeliveryAtUtc = await _db.Deliveries
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        response.DataFreshness.LatestProductionPlanAtUtc = await _db.ProductionPlans
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        response.DataFreshness.LatestSupportRequestAtUtc = await _db.SupportRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);
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
}