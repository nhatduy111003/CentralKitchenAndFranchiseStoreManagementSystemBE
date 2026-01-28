using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class DemandItem
    {
        public Guid DemandAggregationId { get; set; }
        public DemandAggregation Aggregation { get; set; } = null!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public decimal TotalQuantity { get; set; }
    }

}
