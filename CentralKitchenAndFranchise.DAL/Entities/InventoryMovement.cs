namespace CentralKitchenAndFranchise.DAL.Entities;

public class InventoryMovement
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public string Type { get; set; } = default!;
    public decimal Quantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public IngredientBatch Batch { get; set; } = default!;
}
