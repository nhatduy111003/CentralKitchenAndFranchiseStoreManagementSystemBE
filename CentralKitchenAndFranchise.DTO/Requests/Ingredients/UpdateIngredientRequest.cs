namespace CentralKitchenAndFranchise.DTO.Requests.Ingredients;

public class UpdateIngredientRequest
{
    public string Name { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public string Status { get; set; } = "ACTIVE";
}
