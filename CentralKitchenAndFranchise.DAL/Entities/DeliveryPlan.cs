using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class DeliveryPlan
    {
        public int DeliveryPlanId { get; set; }
        public int FranchiseId { get; set; }
        public DateOnly PlannedDate { get; set; }

        public Franchise Franchise { get; set; } = null!;
        public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
    }


}
