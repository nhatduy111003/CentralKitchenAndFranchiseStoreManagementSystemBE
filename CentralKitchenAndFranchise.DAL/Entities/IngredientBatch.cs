namespace CentralKitchenAndFranchise.DAL.Entities;

public class IngredientBatch
{
    public int BatchId { get; set; }
    public int IngredientId { get; set; }
    public string Type { get; set; } = default!;

    public int? FranchiseId { get; set; }
    public Franchise? Franchise { get; set; }

    public int? CentralKitchenId { get; set; }
    public CentralKitchen? CentralKitchen { get; set; }

    public string BatchCode { get; set; } = default!;
    public decimal Quantity { get; set; }
    public DateOnly? ExpiredAt { get; set; }

    public Ingredient Ingredient { get; set; } = default!;
    public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
}
