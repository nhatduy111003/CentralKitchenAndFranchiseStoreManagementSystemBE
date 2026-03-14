// CentralKitchenAndFranchise.BLL/Services/Implementations/AdminDashboardService.cs
using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
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

    public async Task<AdminDashboardOverviewResponse> GetOverviewAsync(AdminDashboardOverviewQuery query, CancellationToken ct = default)
    {
        RequireAdmin();
        query ??= new AdminDashboardOverviewQuery();

        var (fromDate, toDate, tzOffsetMinutes, top, fromUtc, toUtcExclusive) = NormalizeQuery(query);

        var resp = new AdminDashboardOverviewResponse
        {
            FromDate = fromDate,
            ToDate = toDate,
            TimezoneOffsetMinutes = tzOffsetMinutes
        };

        await FillFranchiseSummaryAsync(resp, ct);
        await FillUserSummaryAsync(resp, ct);
        await FillRbacSummaryAsync(resp, ct);

        await FillAuditActivityAsync(resp, fromUtc, toUtcExclusive, top, ct);

        await FillStatusWorkloadAsync(
            resp.StoreOrders,
            _db.StoreOrders.AsNoTracking(),
            x => x.CreatedAt,
            x => x.Status,
            fromUtc,
            toUtcExclusive,
            top,
            ct);

        await FillStatusWorkloadAsync(
            resp.Deliveries,
            _db.Deliveries.AsNoTracking(),
            x => x.CreatedAt,
            x => x.Status,
            fromUtc,
            toUtcExclusive,
            top,
            ct);

        await FillProductionPlanStatusWorkloadAsync(resp.ProductionPlans, fromUtc, toUtcExclusive, top, ct);

        await FillStatusWorkloadAsync(
            resp.SupportRequests,
            _db.SupportRequests.AsNoTracking(),
            x => x.CreatedAt,
            x => x.Status,
            fromUtc,
            toUtcExclusive,
            top,
            ct);

        await FillDataFreshnessAsync(resp, ct);

        return resp;
    }

    private void RequireAdmin()
    {
        if (!_current.IsInRole(RoleNames.Admin))
            throw new ForbiddenAccessException("Admin role required.");
    }

    private static (DateOnly fromDate, DateOnly toDate, int tzOffsetMinutes, int top, DateTime fromUtc, DateTime toUtcExclusive)
        NormalizeQuery(AdminDashboardOverviewQuery query)
    {
        var tzOffsetMinutes = query.TimezoneOffsetMinutes ?? 0;
        if (tzOffsetMinutes is < -14 * 60 or > 14 * 60)
            throw new ArgumentException("timezoneOffsetMinutes must be between -840 and 840.");

        var nowLocal = DateTime.UtcNow.AddMinutes(tzOffsetMinutes);
        var todayLocal = DateOnly.FromDateTime(nowLocal);

        var toDate = query.ToDate ?? todayLocal;
        var fromDate = query.FromDate ?? toDate.AddDays(-6);

        if (fromDate > toDate) throw new ArgumentException("fromDate must be <= toDate.");

        // Protect DB: cap 366 days.
        if (toDate.DayNumber - fromDate.DayNumber > 366)
            throw new ArgumentException("date range too large (max 366 days).");

        var top = query.Top <= 0 ? 10 : query.Top;
        if (top > 50) top = 50;

        // Convert local date range [fromDate..toDate] to UTC instants:
        // fromUtc = local 00:00
        // toUtcExclusive = (toDate + 1) local 00:00
        var fromLocal = fromDate.ToDateTime(TimeOnly.MinValue);
        var toLocalExclusive = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var fromUtc = fromLocal.AddMinutes(-tzOffsetMinutes);
        var toUtcExclusive = toLocalExclusive.AddMinutes(-tzOffsetMinutes);

        return (
            fromDate,
            toDate,
            tzOffsetMinutes,
            top,
            DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(toUtcExclusive, DateTimeKind.Utc)
        );
    }

    private async Task FillFranchiseSummaryAsync(AdminDashboardOverviewResponse resp, CancellationToken ct)
    {
        var grouped = await _db.Franchises.AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        resp.FranchiseSummary.Total = grouped.Sum(x => x.Count);
        resp.FranchiseSummary.Active = grouped.FirstOrDefault(x => x.Status == "ACTIVE")?.Count ?? 0;
        resp.FranchiseSummary.Inactive = grouped.FirstOrDefault(x => x.Status == "INACTIVE")?.Count ?? 0;
    }

    private async Task FillUserSummaryAsync(AdminDashboardOverviewResponse resp, CancellationToken ct)
    {
        var grouped = await _db.Users.AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        resp.UserSummary.Total = grouped.Sum(x => x.Count);
        resp.UserSummary.Active = grouped.FirstOrDefault(x => x.Status == "ACTIVE")?.Count ?? 0;
        resp.UserSummary.Inactive = grouped.FirstOrDefault(x => x.Status == "INACTIVE")?.Count ?? 0;

        var activeByRole = await _db.Users.AsNoTracking()
            .Where(u => u.Status == "ACTIVE")
            .GroupBy(u => u.Role.Name)
            .Select(g => new { RoleName = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        resp.UserSummary.ActiveUsersByRole = activeByRole.ToDictionary(
            x => x.RoleName ?? "UNKNOWN",
            x => x.Count,
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task FillRbacSummaryAsync(AdminDashboardOverviewResponse resp, CancellationToken ct)
    {
        var roleGrouped = await _db.Roles.AsNoTracking()
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        resp.RbacSummary.RoleActiveCount = roleGrouped.FirstOrDefault(x => x.Status == "ACTIVE")?.Count ?? 0;
        resp.RbacSummary.RoleInactiveCount = roleGrouped.FirstOrDefault(x => x.Status == "INACTIVE")?.Count ?? 0;

        var permGrouped = await _db.Permissions.AsNoTracking()
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        resp.RbacSummary.PermissionActiveCount = permGrouped.FirstOrDefault(x => x.Status == "ACTIVE")?.Count ?? 0;
        resp.RbacSummary.PermissionInactiveCount = permGrouped.FirstOrDefault(x => x.Status == "INACTIVE")?.Count ?? 0;

        resp.RbacSummary.RolePermissionLinkCount = await _db.RolePermissions.AsNoTracking().CountAsync(ct);
    }

    private async Task FillAuditActivityAsync(AdminDashboardOverviewResponse resp, DateTime fromUtc, DateTime toUtcExclusive, int top, CancellationToken ct)
    {
        var baseQuery = _db.AuditLogs.AsNoTracking()
            .Where(a => a.CreatedAt >= fromUtc && a.CreatedAt < toUtcExclusive);

        resp.AuditActivity.TotalInRange = await baseQuery.CountAsync(ct);

        resp.AuditActivity.MostRecentAuditAtUtc = await baseQuery
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => (DateTime?)a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        resp.AuditActivity.TopActions = await baseQuery
            .GroupBy(a => a.Action)
            .Select(g => new NamedCount { Name = g.Key ?? "UNKNOWN", Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .Take(top)
            .ToListAsync(ct);

        resp.AuditActivity.TopEntities = await baseQuery
            .GroupBy(a => string.IsNullOrWhiteSpace(a.EntityName) ? "UNKNOWN" : a.EntityName!)
            .Select(g => new NamedCount { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .Take(top)
            .ToListAsync(ct);
    }

    // For entities having Status as string (most of your tables)
    private static async Task FillStatusWorkloadAsync<T>(
        StatusWorkloadSummary target,
        IQueryable<T> source,
        System.Linq.Expressions.Expression<Func<T, DateTime>> createdAtExpr,
        System.Linq.Expressions.Expression<Func<T, string>> statusExpr,
        DateTime fromUtc,
        DateTime toUtcExclusive,
        int top,
        CancellationToken ct)
        where T : class
    {
        var filtered = source.Where(BuildDatePredicate(createdAtExpr, fromUtc, toUtcExclusive));

        target.TotalInRange = await filtered.CountAsync(ct);

        target.TopStatuses = await filtered
            .GroupBy(statusExpr)
            .Select(g => new NamedCount { Name = g.Key ?? "UNKNOWN", Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .Take(top)
            .ToListAsync(ct);
    }

    private async Task FillProductionPlanStatusWorkloadAsync(
    StatusWorkloadSummary target,
    DateTime fromUtc,
    DateTime toUtcExclusive,
    int top,
    CancellationToken ct)
    {
        var filtered = _db.ProductionPlans.AsNoTracking()
            .Where(x => x.CreatedAt >= fromUtc && x.CreatedAt < toUtcExclusive);

        target.TotalInRange = await filtered.CountAsync(ct);

        // Aggregate ở DB (enum key), ToString() sau khi materialize
        var rows = await filtered
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Status) // enum sortable
            .Take(top)
            .ToListAsync(ct);

        target.TopStatuses = rows
            .Select(x => new NamedCount
            {
                Name = x.Status.ToString(),
                Count = x.Count
            })
            .ToList();
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> BuildDatePredicate<T>(
        System.Linq.Expressions.Expression<Func<T, DateTime>> createdAtExpr,
        DateTime fromUtc,
        DateTime toUtcExclusive)
    {
        var p = createdAtExpr.Parameters[0];
        var left = createdAtExpr.Body;

        var ge = System.Linq.Expressions.Expression.GreaterThanOrEqual(left, System.Linq.Expressions.Expression.Constant(fromUtc));
        var lt = System.Linq.Expressions.Expression.LessThan(left, System.Linq.Expressions.Expression.Constant(toUtcExclusive));
        var and = System.Linq.Expressions.Expression.AndAlso(ge, lt);

        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(and, p);
    }

    private async Task FillDataFreshnessAsync(AdminDashboardOverviewResponse resp, CancellationToken ct)
    {
        resp.DataFreshness.LatestAuditLogAtUtc = await _db.AuditLogs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        resp.DataFreshness.LatestUserUpdatedAtUtc = await _db.Users.AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => (DateTime?)x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        resp.DataFreshness.LatestFranchiseUpdatedAtUtc = await _db.Franchises.AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => (DateTime?)x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        resp.DataFreshness.LatestStoreOrderAtUtc = await _db.StoreOrders.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        resp.DataFreshness.LatestDeliveryAtUtc = await _db.Deliveries.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        resp.DataFreshness.LatestProductionPlanAtUtc = await _db.ProductionPlans.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        resp.DataFreshness.LatestSupportRequestAtUtc = await _db.SupportRequests.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}