using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class Recipe
    {
        public Guid RecipeId { get; set; }

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public string Instructions { get; set; } = null!;
        public string Status { get; set; } = "ACTIVE";
    }

}
