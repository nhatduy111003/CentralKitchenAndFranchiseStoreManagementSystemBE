namespace CentralKitchenAndFranchise.DAL.Entities;

public class Recipe
{
    public int RecipeId { get; set; }

    public int ProductId { get; set; }
    public int Version { get; set; }

    // DRAFT | ACTIVE | INACTIVE
    public string Status { get; set; } = "DRAFT";

    // Simple text instructions (manager-maintained)
    public string? Instructions { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Product Product { get; set; } = default!;
}