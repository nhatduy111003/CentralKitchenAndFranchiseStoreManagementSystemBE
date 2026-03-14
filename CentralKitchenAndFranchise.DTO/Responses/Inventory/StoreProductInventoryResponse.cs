using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Responses.Inventory
{
    public class StoreProductInventoryBatchResponse
    {
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = default!;
        public decimal Quantity { get; set; }
        public DateOnly? ExpiredAt { get; set; }
    }

    public class StoreProductInventoryResponse
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = default!;
        public string Sku { get; set; } = default!;
        public string Unit { get; set; } = default!;
        public string ProductType { get; set; } = default!;
        public decimal TotalQuantity { get; set; }
        public DateOnly? EarliestExpiry { get; set; }

        public List<StoreProductInventoryBatchResponse> Batches { get; set; } = new();
    }
}
