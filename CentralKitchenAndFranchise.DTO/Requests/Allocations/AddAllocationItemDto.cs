using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests.Allocations
{
    public class AddAllocationItemDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "FranchiseId phải lớn hơn 0")]
        public int FranchiseId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "ProductId phải lớn hơn 0")]
        public int ProductId { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
        public decimal Quantity { get; set; }
    }

}
