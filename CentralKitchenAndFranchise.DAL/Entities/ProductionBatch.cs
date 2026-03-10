using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class ProductionBatch
    {
        public int ProductionBatchId { get; set; }
        public int ProductionPlanId { get; set; }
        public DateTime CreatedAt { get; set; }

        public ProductionPlan ProductionPlan { get; set; } = null!;
    }

}
