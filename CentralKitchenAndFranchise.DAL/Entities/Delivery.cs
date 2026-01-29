using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class Delivery
    {
        public int DeliveryId { get; set; }
        public int DeliveryPlanId { get; set; }
        public DateTime DeliveredAt { get; set; }

        public DeliveryPlan DeliveryPlan { get; set; } = null!;
        public ICollection<ReceivingReport> ReceivingReports { get; set; } = new List<ReceivingReport>();
    }


}
