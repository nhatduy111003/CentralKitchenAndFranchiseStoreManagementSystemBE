namespace CentralKitchenAndFranchise.DAL.Entities;

public class IngredientBatch
{
    public int Id { get; set; }
    public int IngredientId { get; set; }
    public int FranchiseId { get; set; }
    public string BatchCode { get; set; } = default!;
    public decimal Quantity { get; set; }
    public DateOnly? ExpiredAt { get; set; }

    public Ingredient Ingredient { get; set; } = default!;
    public Franchise Franchise { get; set; } = default!;
    public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
}
