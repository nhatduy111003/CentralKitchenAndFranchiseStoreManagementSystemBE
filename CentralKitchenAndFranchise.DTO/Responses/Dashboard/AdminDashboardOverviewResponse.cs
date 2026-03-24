namespace CentralKitchenAndFranchise.DTO.Responses.Dashboard;

public class AdminDashboardOverviewResponse
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int TimezoneOffsetMinutes { get; set; }

    public CentralKitchenSummary CentralKitchenSummary { get; set; } = new();
    public FranchiseSummary FranchiseSummary { get; set; } = new();
    public UserSummary UserSummary { get; set; } = new();
    public RbacSummary RbacSummary { get; set; } = new();
    public AdminOperationalSnapshotSummary OperationalSnapshot { get; set; } = new();

    // Activity within requested date range.
    public AuditActivitySummary AuditActivity { get; set; } = new();

    // Operational workload grouped by current status, using each module's business date when possible.
    public StatusWorkloadSummary StoreOrders { get; set; } = new();
    public StatusWorkloadSummary Deliveries { get; set; } = new();
    public StatusWorkloadSummary ProductionPlans { get; set; } = new();
    public StatusWorkloadSummary SupportRequests { get; set; } = new();

    // Latest timestamps (UTC) for quick sanity checks.
    public DataFreshnessSummary DataFreshness { get; set; } = new();

    public List<string> Notes { get; set; } = new();
}

public class CentralKitchenSummary
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Inactive { get; set; }
}

public class FranchiseSummary
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Inactive { get; set; }
}

public class UserSummary
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Inactive { get; set; }

    // Role.Name -> count (ACTIVE users only)
    public Dictionary<string, int> ActiveUsersByRole { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class RbacSummary
{
    public int RoleActiveCount { get; set; }
    public int RoleInactiveCount { get; set; }

    public int PermissionActiveCount { get; set; }
    public int PermissionInactiveCount { get; set; }

    public int RolePermissionLinkCount { get; set; }
}

public class AdminOperationalSnapshotSummary
{
    public int OpenStoreOrdersCount { get; set; }
    public int ActiveProductionPlansCount { get; set; }
    public int OpenDeliveriesCount { get; set; }
    public int PendingReceivingCount { get; set; }
}

public class AuditActivitySummary
{
    public int TotalInRange { get; set; }
    public List<NamedCount> TopActions { get; set; } = new();
    public List<NamedCount> TopEntities { get; set; } = new();
    public DateTime? MostRecentAuditAtUtc { get; set; }
}

public class StatusWorkloadSummary
{
    public int TotalInRange { get; set; }
    public List<NamedCount> TopStatuses { get; set; } = new();
}

public class DataFreshnessSummary
{
    public DateTime? LatestAuditLogAtUtc { get; set; }
    public DateTime? LatestUserUpdatedAtUtc { get; set; }
    public DateTime? LatestFranchiseUpdatedAtUtc { get; set; }
    public DateTime? LatestCentralKitchenUpdatedAtUtc { get; set; }

    public DateTime? LatestStoreOrderAtUtc { get; set; }
    public DateTime? LatestDeliveryAtUtc { get; set; }
    public DateTime? LatestProductionPlanAtUtc { get; set; }
    public DateTime? LatestSupportRequestAtUtc { get; set; }
}

public class NamedCount
{
    public string Name { get; set; } = default!;
    public int Count { get; set; }
}