using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests
{
    public class AdjustIngredientInventoryDto
    {
        public int BatchId { get; set; }

        // "ADJUST" or "WASTE"
        public string Type { get; set; } = "ADJUST";

        // +increase / -decrease
        public decimal DeltaQuantity { get; set; }

        public string Reason { get; set; } = default!;

        // optional: link to production plan / doc
        public string? Reference { get; set; }
    }
}
