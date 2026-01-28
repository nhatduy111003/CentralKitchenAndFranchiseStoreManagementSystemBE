using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class ProductionPlan
    {
        public Guid ProductionPlanId { get; set; }

        public Guid KitchenFranchiseId { get; set; }
        public Franchise Kitchen { get; set; } = null!;

        public string Status { get; set; } = "DRAFT";
        public DateTime CreatedAt { get; set; }

        public ICollection<ProductionPlanItem> Items { get; set; } = new List<ProductionPlanItem>();
    }

}
