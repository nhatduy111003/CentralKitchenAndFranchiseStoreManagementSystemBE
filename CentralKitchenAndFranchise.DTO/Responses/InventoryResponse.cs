using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Responses
{
    public class IssueIngredientsByProductionPlanResponse
    {
        public int ProductionPlanId { get; set; }
        public int FranchiseId { get; set; }
        public DateOnly PlanDate { get; set; }
        public DateTime IssuedAt { get; set; }

        public List<IssuedIngredientLine> Lines { get; set; } = new();
    }

    public class IssuedIngredientLine
    {
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = "";
        public decimal RequiredQuantity { get; set; }

        // FEFO picked batches
        public List<IssuedBatchLine> Batches { get; set; } = new();
    }

    public class IssuedBatchLine
    {
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = "";
        public DateOnly? ExpiredAt { get; set; }
        public decimal IssuedQuantity { get; set; }
        public int MovementId { get; set; }
    }
}
