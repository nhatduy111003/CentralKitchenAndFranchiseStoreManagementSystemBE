using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class StoreOrderItem
    {
        public int StoreOrderItemId { get; set; }
        public int StoreOrderId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }

        public StoreOrder StoreOrder { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }

}
