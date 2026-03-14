using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class DemandAggregation
    {
        public int DemandAggregationId { get; set; }
        public DateOnly PlanDate { get; set; }
        public int CentralKitchenId { get; set; }
        public DateTime CreatedAt { get; set; }

        public CentralKitchen CentralKitchen { get; set; } = null!;
        public ICollection<DemandItem> DemandItems { get; set; } = new List<DemandItem>();
        public ICollection<Allocation> Allocations { get; set; } = new List<Allocation>();
    }



}
