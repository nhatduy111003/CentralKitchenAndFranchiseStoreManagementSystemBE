using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class ProductionRun
    {
        public int ProductionRunId { get; set; }

        public int ProductionPlanId { get; set; }
        public ProductionPlan ProductionPlan { get; set; } = null!;

        public int CentralKitchenId { get; set; }
        public CentralKitchen CentralKitchen { get; set; } = null!;

        public string RunCode { get; set; } = default!;

        public DateOnly ProductionDate { get; set; }

        public decimal Quantity { get; set; }

        public string Status { get; set; } = default!;

        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public ICollection<ProductBatch> ProductBatches { get; set; } = new List<ProductBatch>();
    }

}
