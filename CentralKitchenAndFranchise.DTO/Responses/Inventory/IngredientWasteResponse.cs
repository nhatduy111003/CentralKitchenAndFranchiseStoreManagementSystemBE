using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Responses.Inventory
{
    public class IngredientWasteResponse
    {
        public int BatchId { get; set; }
        public int MovementId { get; set; }
        public int FranchiseId { get; set; }
        public int IngredientId { get; set; }
        public string BatchCode { get; set; } = default!;
        public DateOnly? ExpiredAt { get; set; }

        public decimal BeforeQuantity { get; set; }
        public decimal WasteQuantity { get; set; }
        public decimal AfterQuantity { get; set; }

        public string Reason { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
    }
}
