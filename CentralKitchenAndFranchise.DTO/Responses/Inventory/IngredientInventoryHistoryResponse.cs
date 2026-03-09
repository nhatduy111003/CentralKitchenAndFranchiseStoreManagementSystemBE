using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Responses.Inventory
{
    public class IngredientInventoryHistoryResponse
    {
        public int MovementId { get; set; }
        public int BatchId { get; set; }
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = default!;
        public string Unit { get; set; } = default!;
        public string BatchCode { get; set; } = default!;
        public DateOnly? ExpiredAt { get; set; }

        public string Type { get; set; } = default!;
        public decimal Quantity { get; set; }

        public int? DeliveryId { get; set; }
        public int? CreatedByUserId { get; set; }
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
