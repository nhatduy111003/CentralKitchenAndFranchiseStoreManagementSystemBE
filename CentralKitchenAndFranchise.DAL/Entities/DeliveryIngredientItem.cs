namespace CentralKitchenAndFranchise.DAL.Entities;

public class DeliveryIngredientItem
{
    public int DeliveryIngredientItemId { get; set; }

    public int DeliveryId { get; set; }
    public int IngredientId { get; set; }
    public decimal Quantity { get; set; }

    public Delivery Delivery { get; set; } = default!;
    public Ingredient Ingredient { get; set; } = default!;
}
