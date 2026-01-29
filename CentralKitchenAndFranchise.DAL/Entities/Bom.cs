using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class Bom
    {
        public int BomId { get; set; }
        public int ProductId { get; set; }
        public int Version { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        public Product Product { get; set; } = null!;
        public ICollection<BomItem> Items { get; set; } = new List<BomItem>();
    }

}
