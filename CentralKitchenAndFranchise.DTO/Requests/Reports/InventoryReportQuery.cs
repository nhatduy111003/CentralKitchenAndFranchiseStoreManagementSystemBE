namespace CentralKitchenAndFranchise.DTO.Requests.Reports;

public class InventoryReportQuery
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    // Null => kitchen scope. When the system has multiple central kitchens, FE should pass CentralKitchenId.
    public int? FranchiseId { get; set; }

    // Optional because the current domain model supports multiple central kitchens.
    public int? CentralKitchenId { get; set; }

    // Minutes offset from UTC. Default Vietnam business timezone.
    public int? TimezoneOffsetMinutes { get; set; } = 420;
}
