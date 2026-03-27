namespace CentralKitchenAndFranchise.DTO.Responses.Reports;

public class StorePerformanceReportResponse
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int TimezoneOffsetMinutes { get; set; }

    public List<string> Notes { get; set; } = new();
    public List<StorePerformanceReportItemResponse> Items { get; set; } = new();
}

public class StorePerformanceReportItemResponse
{
    public int FranchiseId { get; set; }
    public string FranchiseName { get; set; } = default!;

    public int TotalOrderCount { get; set; }

    public decimal TotalIngredientSpending { get; set; }
    public decimal TotalProductSpending { get; set; }
    public decimal TotalSpending { get; set; }

    public int TotalDeliveredOrders { get; set; }
    public int OnTimeDeliveredOrders { get; set; }

    // Percentage value between 0 and 100.
    public decimal? OnTimeRate { get; set; }
}
