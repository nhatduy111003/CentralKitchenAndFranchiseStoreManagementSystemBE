namespace CentralKitchenAndFranchise.DTO.Responses.Ingredients;

public class IngredientResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
