using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class Delivery
    {
        public Guid DeliveryId { get; set; }

        public Guid DeliveryPlanId { get; set; }
        public DeliveryPlan DeliveryPlan { get; set; } = null!;

        public Guid FranchiseId { get; set; }
        public Franchise Franchise { get; set; } = null!;

        public string Status { get; set; } = "PENDING";

        public ICollection<DeliveryItem> Items { get; set; } = new List<DeliveryItem>();
    }

}
