using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class InventoryMovement
    {
        public int MovementId { get; set; }
        public int BatchId { get; set; }
        public string Type { get; set; } = null!;
        public decimal Quantity { get; set; }
        public DateTime CreatedAt { get; set; }

        public IngredientBatch Batch { get; set; } = null!;
    }

}
