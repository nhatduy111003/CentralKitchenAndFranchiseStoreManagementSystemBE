namespace CentralKitchenAndFranchise.DTO.Responses.Recipes;

public class RecipeResponse
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = default!;
    public string? Instructions { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}