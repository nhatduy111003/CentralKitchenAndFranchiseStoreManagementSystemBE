namespace CentralKitchenAndFranchise.DTO.Requests.Ingredients;

public class UpdateIngredientRequest
{
    public string Name { get; set; } = default!;
    public string Unit { get; set; } = default!;

    public decimal SafetyStock { get; set; } = 0;
    public decimal WasteThreshold { get; set; } = 0;
}
