namespace CentralKitchenAndFranchise.DTO.Requests.Dashboard;

public class KitchenDashboardOverviewQuery
{
    // Admin/Manager must pass centralKitchenId; KitchenStaff uses assigned central kitchen.
    public int? CentralKitchenId { get; set; }

    // Local date (yyyy-MM-dd). If null -> default last 7 days.
    public DateOnly? FromDate { get; set; }

    // Local date (yyyy-MM-dd). If null -> today.
    public DateOnly? ToDate { get; set; }

    // Minutes offset from UTC. Example: Vietnam = 420.
    public int? TimezoneOffsetMinutes { get; set; }

    // Limit for alert/action lists. Default 10, max 100.
    public int Limit { get; set; } = 10;
}