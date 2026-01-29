using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class ProductionPlan
    {
        public int ProductionPlanId { get; set; }
        public DateOnly PlanDate { get; set; }
        public int FranchiseId { get; set; }
        public DateTime CreatedAt { get; set; }

        public Franchise Franchise { get; set; } = null!;
        public ICollection<ProductionPlanItem> ProductionPlanItems { get; set; } = new List<ProductionPlanItem>();
    }


}
