namespace CentralKitchenAndFranchise.DTO.Responses.Ingredients;

public class IngredientResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public string Status { get; set; } = default!;

    public decimal SafetyStock { get; set; }
    public decimal WasteThreshold { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
