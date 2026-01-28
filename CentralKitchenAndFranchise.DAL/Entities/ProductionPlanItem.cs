using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class ProductionPlanItem
    {
        public Guid ProductionPlanId { get; set; }
        public ProductionPlan ProductionPlan { get; set; } = null!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public decimal Quantity { get; set; }
    }

}
