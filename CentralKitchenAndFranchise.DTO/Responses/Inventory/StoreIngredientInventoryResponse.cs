using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Responses.Inventory
{
    public class StoreIngredientInventoryBatchResponse
    {
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = default!;
        public decimal Quantity { get; set; }
        public DateOnly? ExpiredAt { get; set; }
    }

    public class StoreIngredientInventoryResponse
    {
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = default!;
        public string Unit { get; set; } = default!;
        public decimal TotalQuantity { get; set; }
        public DateOnly? EarliestExpiry { get; set; }

        public List<StoreIngredientInventoryBatchResponse> Batches { get; set; } = new();
    }
}
