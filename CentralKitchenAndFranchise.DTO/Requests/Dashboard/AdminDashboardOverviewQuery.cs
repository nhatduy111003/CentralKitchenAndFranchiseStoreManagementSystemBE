namespace CentralKitchenAndFranchise.DTO.Requests.Dashboard;

public class AdminDashboardOverviewQuery
{
    // Local date (yyyy-MM-dd). If null -> default last 7 days.
    public DateOnly? FromDate { get; set; }

    // Local date (yyyy-MM-dd). If null -> today.
    public DateOnly? ToDate { get; set; }

    // Minutes offset from UTC. Example: Vietnam = 420.
    // If null -> assume 0 (UTC).
    public int? TimezoneOffsetMinutes { get; set; }

    // Top N for breakdown lists (actions/entities/statuses). Default 10, max 50.
    public int Top { get; set; } = 10;
}