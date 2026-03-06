using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests.Allocations
{
    public class AddAllocationItemDto
    {
        public int FranchiseId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
    }

}
