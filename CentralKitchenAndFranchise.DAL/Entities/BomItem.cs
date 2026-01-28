namespace CentralKitchenAndFranchise.DAL.Entities;

public class BomItem
{
    public int Id { get; set; }
    public int BomId { get; set; }
    public int IngredientId { get; set; }
    public decimal Quantity { get; set; }

    public Bom Bom { get; set; } = default!;
    public Ingredient Ingredient { get; set; } = default!;
}
