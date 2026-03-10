using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests.Inventory
{
    public class CreateIngredientWasteDto
    {
        public int BatchId { get; set; }
        public decimal Quantity { get; set; }
        public string Reason { get; set; } = default!;
        public string? Reference { get; set; }
    }
}
