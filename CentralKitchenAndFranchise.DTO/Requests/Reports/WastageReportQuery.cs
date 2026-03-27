namespace CentralKitchenAndFranchise.DTO.Requests.Reports;

public class WastageReportQuery
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    // Optional single-franchise scope.
    public int? FranchiseId { get; set; }

    // Optional central-kitchen scope.
    public int? CentralKitchenId { get; set; }

    // lostValue | wastedQuantity | wasteRate
    public string? SortBy { get; set; } = "lostValue";

    // Minutes offset from UTC. Default Vietnam business timezone.
    public int? TimezoneOffsetMinutes { get; set; } = 420;
}
