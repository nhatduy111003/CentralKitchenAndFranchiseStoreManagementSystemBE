namespace CentralKitchenAndFranchise.DTO.Requests.Inventory;

public class AdjustProductInventoryDto
{
    public int BatchId { get; set; }

    // "ADJUST" or "WASTE"
    public string Type { get; set; } = "ADJUST";

    // +increase / -decrease
    public decimal DeltaQuantity { get; set; }

    public string Reason { get; set; } = default!;

    public string? Reference { get; set; }
}