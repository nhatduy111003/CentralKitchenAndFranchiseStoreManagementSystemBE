using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Responses.ProductionPlans
{
    public class ProductionPlanResponse
    {
        public int ProductionPlanId { get; set; }
        public int FranchiseId { get; set; }
        public DateOnly PlanDate { get; set; }
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        public List<ProductionPlanItemResponse> Items { get; set; } = new();
    }

    public class ProductionPlanItemResponse
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public string Unit { get; set; } = "";
        public decimal Quantity { get; set; }
    }
}
