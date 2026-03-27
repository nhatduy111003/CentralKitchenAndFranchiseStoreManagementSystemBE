namespace CentralKitchenAndFranchise.DTO.Responses.Reports;

public class WastageReportResponse
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int TimezoneOffsetMinutes { get; set; }

    public string ScopeType { get; set; } = default!;
    public int? FranchiseId { get; set; }
    public string? FranchiseName { get; set; }
    public int? CentralKitchenId { get; set; }
    public string? CentralKitchenName { get; set; }
    public string SortBy { get; set; } = default!;

    public List<string> Notes { get; set; } = new();
    public List<WastageReportItemResponse> Items { get; set; } = new();
}

public class WastageReportItemResponse
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public string WasteReason { get; set; } = default!;
    public decimal WastedQuantity { get; set; }

    // Percentage value between 0 and 100.
    public decimal? WasteRate { get; set; }

    public decimal TotalLostValue { get; set; }
}
