namespace CentralKitchenAndFranchise.DTO.Responses.Inventory;

public class AdjustProductInventoryResponse
{
    public int BatchId { get; set; }
    public int MovementId { get; set; }

    public int? FranchiseId { get; set; }
    public int? CentralKitchenId { get; set; }

    public int ProductId { get; set; }
    public string BatchCode { get; set; } = default!;
    public DateOnly? ExpiredAt { get; set; }

    public decimal BeforeQuantity { get; set; }
    public decimal DeltaQuantity { get; set; }
    public decimal AfterQuantity { get; set; }

    public string Type { get; set; } = default!;
    public string Reason { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}