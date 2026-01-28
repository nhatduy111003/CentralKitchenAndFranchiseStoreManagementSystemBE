using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class InventoryMovement
    {
        public Guid InventoryMovementId { get; set; }

        public Guid IngredientBatchId { get; set; }
        public IngredientBatch Batch { get; set; } = null!;

        public string Type { get; set; } = null!; // IN, OUT, ADJUST, WASTE
        public decimal Quantity { get; set; }
        public string Reason { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }

}
