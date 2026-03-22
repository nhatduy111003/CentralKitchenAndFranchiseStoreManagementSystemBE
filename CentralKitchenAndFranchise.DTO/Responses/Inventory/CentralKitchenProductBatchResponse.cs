namespace CentralKitchenAndFranchise.DTO.Responses.Inventory;

public class CentralKitchenProductBatchResponse
{
    public int BatchId { get; set; }
    public int CentralKitchenId { get; set; }

    public int ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public string Unit { get; set; } = default!;

    public string BatchCode { get; set; } = default!;
    public decimal Quantity { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateOnly? ExpiredAt { get; set; }
}