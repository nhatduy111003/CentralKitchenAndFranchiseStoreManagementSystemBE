using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests
{
    public class CreateIngredientInboundDto
    {
        public int IngredientId { get; set; }
        public string BatchCode { get; set; } = default!;
        public DateOnly ExpiredAt { get; set; }
        public decimal Quantity { get; set; }
        public string? Reason { get; set; }
    }
}
