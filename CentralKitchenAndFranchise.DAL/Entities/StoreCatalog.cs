using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class StoreCatalog
    {
        public int FranchiseId { get; set; }
        public int ProductId { get; set; }
        public decimal Price { get; set; }

        public Franchise Franchise { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }


}
