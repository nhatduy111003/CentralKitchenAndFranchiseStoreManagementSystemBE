using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class StoreOrder
    {
        public Guid StoreOrderId { get; set; }

        public Guid FranchiseId { get; set; }
        public Franchise Franchise { get; set; } = null!;

        public DateOnly OrderDate { get; set; }
        public string Status { get; set; } = "DRAFT";
        public DateTime CreatedAt { get; set; }

        public ICollection<StoreOrderItem> Items { get; set; } = new List<StoreOrderItem>();
    }

}
