namespace CentralKitchenAndFranchise.DAL.Entities;

public class Ingredient
{
    public int IngredientId { get; set; }
    public string Name { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public string Status { get; set; } = "ACTIVE";

    public decimal SafetyStock { get; set; } = 0;
    public decimal WasteThreshold { get; set; } = 0;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<BomItem> BomItems { get; set; } = new List<BomItem>();
    public ICollection<IngredientBatch> IngredientBatches { get; set; } = new List<IngredientBatch>();
}
