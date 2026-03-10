namespace CentralKitchenAndFranchise.DTO.Requests.Dashboard;

public class ManagerDashboardOverviewQuery
{
    // Local date (yyyy-MM-dd). If null -> default last 7 days.
    public DateOnly? FromDate { get; set; }

    // Local date (yyyy-MM-dd). If null -> today.
    public DateOnly? ToDate { get; set; }

    // Minutes offset from UTC. Example: Vietnam = 420.
    // If null -> assume 0 (UTC).
    public int? TimezoneOffsetMinutes { get; set; }

    // Limit for alert lists. Default 20, max 200.
    public int Limit { get; set; } = 20;
}