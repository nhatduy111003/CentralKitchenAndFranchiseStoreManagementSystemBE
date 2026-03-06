using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Responses
{
    public class AdjustIngredientInventoryResponse
    {
        public int BatchId { get; set; }
        public int MovementId { get; set; }

        public int FranchiseId { get; set; }
        public int IngredientId { get; set; }
        public string BatchCode { get; set; } = "";
        public DateOnly? ExpiredAt { get; set; }

        public decimal BeforeQuantity { get; set; }
        public decimal DeltaQuantity { get; set; }
        public decimal AfterQuantity { get; set; }

        public string Type { get; set; } = "";
        public string Reason { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
