namespace CentralKitchenAndFranchise.DTO.Requests.Ingredients;

public class CreateIngredientRequest
{
    public string Name { get; set; } = default!;
    public string Unit { get; set; } = default!;
}
