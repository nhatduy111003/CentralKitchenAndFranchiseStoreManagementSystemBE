using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class DeliveryPlan
    {
        public Guid DeliveryPlanId { get; set; }
        public DateOnly PlannedDate { get; set; }
        public string Status { get; set; } = "DRAFT";
    }

}
