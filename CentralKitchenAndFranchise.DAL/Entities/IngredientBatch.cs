using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class IngredientBatch
    {
        public Guid IngredientBatchId { get; set; }

        public Guid IngredientId { get; set; }
        public Ingredient Ingredient { get; set; } = null!;

        public string BatchCode { get; set; } = null!;
        public DateOnly ExpiryDate { get; set; }
        public decimal Quantity { get; set; }

        public Guid FranchiseId { get; set; }
        public Franchise Franchise { get; set; } = null!;
    }

}
