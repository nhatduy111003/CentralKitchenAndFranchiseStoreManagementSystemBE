using CentralKitchenAndFranchise.DTO.Requests.ProductionPlanItems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests.ProductionPlans
{
    public class CreateProductionPlanDto
    {
        public DateOnly PlanDate { get; set; }
    }

    public class UpdateProductionPlanStatusDto
    {
        public string Status { get; set; } = default!;   // DRAFT/CONFIRMED/IN_PROGRESS/COMPLETED/CANCELLED
        public string? Reason { get; set; }
    }
}
