using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class Allocation
    {
        public Guid AllocationId { get; set; }

        public Guid DemandAggregationId { get; set; }
        public DemandAggregation Aggregation { get; set; } = null!;

        public Guid ApprovedBy { get; set; }
        public User Approver { get; set; } = null!;

        public DateTime ApprovedAt { get; set; }

        public ICollection<AllocationItem> Items { get; set; } = new List<AllocationItem>();
    }

}
