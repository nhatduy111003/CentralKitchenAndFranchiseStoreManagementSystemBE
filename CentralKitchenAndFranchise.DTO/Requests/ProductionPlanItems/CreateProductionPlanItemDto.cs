using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests.ProductionPlanItems
{
    public class CreateProductionPlanItemDto
    {

        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
    }
}
