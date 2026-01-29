using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class BomItem
    {
        public int BomItemId { get; set; }
        public int BomId { get; set; }
        public int IngredientId { get; set; }
        public decimal Quantity { get; set; }

        public Bom Bom { get; set; } = null!;
        public Ingredient Ingredient { get; set; } = null!;
    }

}
