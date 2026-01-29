namespace CentralKitchenAndFranchise.DAL.Entities;

public class InventoryMovement
{
    public int MovementId { get; set; }
    public int BatchId { get; set; }

    public string Type { get; set; } = default!;
    public decimal Quantity { get; set; }

    // migration full dùng DateTime (timestamptz)
    public DateTime CreatedAt { get; set; }

    public IngredientBatch Batch { get; set; } = default!;
}
