using System.ComponentModel.DataAnnotations.Schema;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class Recipe
    {
        public int RecipeId { get; set; }
        public int ProductId { get; set; }
        public int Version { get; set; }
        public string Status { get; set; } = "DRAFT";
        public DateTime CreatedAt { get; set; }

        // DB hiện tại KHÔNG có 2 cột này
        [NotMapped]
        public DateTime UpdatedAt { get; set; }

        [NotMapped]
        public string? Instructions { get; set; }
    }
}