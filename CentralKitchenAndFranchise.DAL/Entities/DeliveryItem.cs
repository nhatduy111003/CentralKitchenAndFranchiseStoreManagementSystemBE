using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class DeliveryItem
    {
        public int DeliveryItemId { get; set; }
        public int DeliveryId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }

        public Delivery Delivery { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }


}
