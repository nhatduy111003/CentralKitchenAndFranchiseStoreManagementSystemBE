using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class StoreOrder
    {
        public int StoreOrderId { get; set; }
        public int FranchiseId { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        public Franchise Franchise { get; set; } = null!;
        public ICollection<StoreOrderItem> Items { get; set; } = new List<StoreOrderItem>();
    }
}
