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
        public DateTime CreatedAt { get; set; }

        public ICollection<DemandItem> DemandItems { get; set; } = new List<DemandItem>();
    }



}
