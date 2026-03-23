namespace CentralKitchenAndFranchise.DTO.Responses.Inventory;

public class FranchiseIngredientBatchResponse
{
    public int BatchId { get; set; }
    public int FranchiseId { get; set; }

    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = default!;
    public string Unit { get; set; } = default!;

    public string BatchCode { get; set; } = default!;
    public decimal Quantity { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateOnly? ExpiredAt { get; set; }
}