using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class Bom
    {
        public Guid BomId { get; set; }

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public int Version { get; set; }
        public string Status { get; set; } = "DRAFT";
        public DateTime CreatedAt { get; set; }

        public ICollection<BomItem> Items { get; set; } = new List<BomItem>();
    }

}
