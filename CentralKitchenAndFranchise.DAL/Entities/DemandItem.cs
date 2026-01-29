using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class DemandItem
    {
        public int DemandItemId { get; set; }
        public int DemandAggregationId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }

        public DemandAggregation DemandAggregation { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }



}
