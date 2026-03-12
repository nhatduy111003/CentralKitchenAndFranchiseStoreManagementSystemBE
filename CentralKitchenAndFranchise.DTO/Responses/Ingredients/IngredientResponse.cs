namespace CentralKitchenAndFranchise.DTO.Responses.Ingredients;

public class IngredientResponse
{
    public int Id { get; set; }
    public int? SupplierId { get; set; }

    public string Name { get; set; } = default!;
    public string? SupplierName { get; set; }
    public string Unit { get; set; } = default!;
    public string Status { get; set; } = default!;

    public decimal Price { get; set; }         
    public decimal SafetyStock { get; set; }
    public decimal WasteThreshold { get; set; }

    public int ShelfLifeDays { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
