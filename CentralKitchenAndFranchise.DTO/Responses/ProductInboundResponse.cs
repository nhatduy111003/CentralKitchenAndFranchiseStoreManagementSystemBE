using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Responses
{
    public class ProductInboundResponse
    {
        public int BatchId { get; set; }
        public int FranchiseId { get; set; }
        public int ProductId { get; set; }
        public string BatchCode { get; set; } = "";
        public DateOnly? ExpiredAt { get; set; }
        public decimal Quantity { get; set; }

        public int CreatedMovementId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
