using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class AllocationItem
    {
        public Guid AllocationId { get; set; }
        public Allocation Allocation { get; set; } = null!;

        public Guid FranchiseId { get; set; }
        public Franchise Franchise { get; set; } = null!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public decimal AllocatedQuantity { get; set; }
    }

}
