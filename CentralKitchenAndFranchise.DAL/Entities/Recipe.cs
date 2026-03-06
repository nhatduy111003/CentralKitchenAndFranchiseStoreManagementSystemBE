namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class Recipe
    {
        public int RecipeId { get; set; }
        public int ProductId { get; set; }
        public int Version { get; set; }
        public string Status { get; set; } = "DRAFT";

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public string? Instructions { get; set; }
    }
}