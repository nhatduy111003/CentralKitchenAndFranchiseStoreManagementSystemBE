namespace CentralKitchenAndFranchise.DTO.Responses.Inventory;

public class InventoryBatchQuantityResponse
{
    public int BatchId { get; set; }
    public string BatchCode { get; set; } = default!;
    public decimal Quantity { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateOnly? ExpiredAt { get; set; }
}