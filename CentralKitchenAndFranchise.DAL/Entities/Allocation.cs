using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class Allocation
    {
        public int AllocationId { get; set; }
        public int DemandAggregationId { get; set; }
        public DateTime CreatedAt { get; set; }

        public DemandAggregation DemandAggregation { get; set; } = null!;
        public ICollection<AllocationItem> AllocationItems { get; set; } = new List<AllocationItem>();
    }




}
