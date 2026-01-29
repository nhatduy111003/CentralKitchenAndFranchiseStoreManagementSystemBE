using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class Delivery
    {
        public int Id { get; set; }
        public int StoreFranchiseId { get; set; }

        public int DeliveryPlanId { get; set; }
        public DateTime DeliveredAt { get; set; }

        public Franchise StoreFranchise { get; set; } = default!;
        public DeliveryPlan DeliveryPlan { get; set; } = default!;
        public ICollection<ReceivingReport> ReceivingReports { get; set; } = new List<ReceivingReport>();
    }


}
