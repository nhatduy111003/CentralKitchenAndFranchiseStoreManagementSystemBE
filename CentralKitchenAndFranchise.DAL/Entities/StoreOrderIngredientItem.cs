namespace CentralKitchenAndFranchise.DAL.Entities;

public class StoreOrderIngredientItem
{
    public int StoreOrderIngredientItemId { get; set; }
    public int StoreOrderId { get; set; }
    public int IngredientId { get; set; }

    public decimal Quantity { get; set; }

    public StoreOrder StoreOrder { get; set; } = default!;
    public Ingredient Ingredient { get; set; } = default!;
}