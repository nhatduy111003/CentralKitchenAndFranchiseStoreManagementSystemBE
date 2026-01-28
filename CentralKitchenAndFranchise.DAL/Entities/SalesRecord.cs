using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class SalesRecord
    {
        public Guid SalesRecordId { get; set; }

        public Guid FranchiseId { get; set; }
        public Franchise Franchise { get; set; } = null!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public decimal Quantity { get; set; }
        public DateTime SoldAt { get; set; }
    }

}
