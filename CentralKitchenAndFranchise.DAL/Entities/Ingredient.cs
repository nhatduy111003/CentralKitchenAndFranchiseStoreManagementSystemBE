using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class Ingredient
    {
        public Guid IngredientId { get; set; }

        public string Name { get; set; } = null!;
        public string Unit { get; set; } = null!;
        public string Status { get; set; } = "ACTIVE";

        public DateTime CreatedAt { get; set; }
    }

}
