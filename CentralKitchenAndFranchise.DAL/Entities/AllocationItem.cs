using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class AllocationItem
    {
        public int AllocationItemId { get; set; }
        public int AllocationId { get; set; }
        public int FranchiseId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }

        public Allocation Allocation { get; set; } = null!;
        public Franchise Franchise { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }


}
