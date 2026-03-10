namespace CentralKitchenAndFranchise.DAL.Entities;

public class InventoryMovement
{
    public int MovementId { get; set; }
    public int BatchId { get; set; }

    // IN / OUT / WASTE / ADJUST
    public string Type { get; set; } = default!;
    public decimal Quantity { get; set; }

    // Trace
    public int? CreatedByUserId { get; set; }
    public string? Reason { get; set; }
    public int? DeliveryId { get; set; }

    public DateTime CreatedAt { get; set; }

    public IngredientBatch Batch { get; set; } = default!;
}
