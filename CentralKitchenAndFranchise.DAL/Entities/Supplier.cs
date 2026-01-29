using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class Supplier
    {
        public int SupplierId { get; set; }
        public string Name { get; set; } = null!;
        public string? ContactInfo { get; set; }
        public string Status { get; set; } = "ACTIVE";
    }


}
