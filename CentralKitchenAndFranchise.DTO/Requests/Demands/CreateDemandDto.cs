using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests.Demands
{
    public class CreateDemandDto
    {
        public DateOnly PlanDate { get; set; }
        public int? CentralKitchenId { get; set; }
    }

}
