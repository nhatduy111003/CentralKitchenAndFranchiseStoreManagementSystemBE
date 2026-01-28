using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class DemandAggregation
    {
        public Guid DemandAggregationId { get; set; }
        public DateTime RunAt { get; set; }

        public ICollection<DemandItem> Items { get; set; } = new List<DemandItem>();
    }

}
