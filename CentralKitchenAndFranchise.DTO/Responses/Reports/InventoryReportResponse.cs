namespace CentralKitchenAndFranchise.DTO.Responses.Reports;

public class InventoryReportResponse
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int TimezoneOffsetMinutes { get; set; }

    public string ScopeType { get; set; } = default!;
    public int? FranchiseId { get; set; }
    public string? FranchiseName { get; set; }
    public int? CentralKitchenId { get; set; }
    public string? CentralKitchenName { get; set; }

    public List<string> Notes { get; set; } = new();
    public List<InventoryReportItemResponse> Items { get; set; } = new();
}

public class InventoryReportItemResponse
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public string ItemType { get; set; } = default!;

    public decimal OpeningQuantity { get; set; }
    public decimal InboundQuantity { get; set; }
    public decimal OutboundQuantity { get; set; }
    public decimal WastedQuantity { get; set; }

    // Added because current movement schema stores ADJUST without direction on movement rows.
    public decimal AdjustmentQuantity { get; set; }

    public decimal ClosingQuantity { get; set; }
    public decimal ClosingValue { get; set; }
}
