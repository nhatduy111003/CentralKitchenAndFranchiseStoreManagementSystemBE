namespace CentralKitchenAndFranchise.DTO.Responses.Inventory;

public class FranchiseInventorySummaryResponse
{
    public List<FranchiseInventorySummaryItemResponse> Items { get; set; } = [];
}

public class FranchiseInventorySummaryItemResponse
{
    public string ItemType { get; set; } = default!;
    public int ItemId { get; set; }
    public string ItemName { get; set; } = default!;
    public string Unit { get; set; } = default!;

    public decimal TotalQuantity { get; set; }
    public decimal? LowStockThreshold { get; set; }
    public bool IsLowStock { get; set; }

    public List<FranchiseInventoryBatchResponse> Batches { get; set; } = [];
}

public class FranchiseInventoryBatchResponse
{
    public int BatchId { get; set; }
    public string BatchCode { get; set; } = default!;
    public DateOnly? ExpiredAt { get; set; }
    public decimal Quantity { get; set; }
}