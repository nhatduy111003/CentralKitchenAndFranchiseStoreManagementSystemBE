namespace CentralKitchenAndFranchise.DAL.Entities;

public class DeliveryIngredientItem
{
    public int DeliveryIngredientItemId { get; set; }

    public int DeliveryId { get; set; }
    public int IngredientId { get; set; }

    // actual forwarded quantity
    public decimal Quantity { get; set; }

    // original locked order quantity
    public decimal RequestedQuantity { get; set; }

    public bool IsDropped { get; set; }
    public string? DropReason { get; set; }

    public Delivery Delivery { get; set; } = default!;
    public Ingredient Ingredient { get; set; } = default!;
}