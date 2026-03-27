namespace CentralKitchenAndFranchise.DTO.Requests.Reports;

public class StorePerformanceReportQuery
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    // Minutes offset from UTC. Default Vietnam business timezone.
    public int? TimezoneOffsetMinutes { get; set; } = 420;
}
