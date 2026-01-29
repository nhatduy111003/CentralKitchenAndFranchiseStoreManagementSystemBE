using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class IngredientBatch
    {
        public int BatchId { get; set; }
        public int IngredientId { get; set; }
        public int FranchiseId { get; set; }
        public string BatchCode { get; set; } = null!;
        public decimal Quantity { get; set; }
        public DateOnly? ExpiredAt { get; set; }

        public Ingredient Ingredient { get; set; } = null!;
        public Franchise Franchise { get; set; } = null!;
    }

}
