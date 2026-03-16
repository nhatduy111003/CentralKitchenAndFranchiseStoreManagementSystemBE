namespace CentralKitchenAndFranchise.DTO.Responses.ProductionPlans;

public class ProductionPlanConflictData
{
    public int ExistingProductionPlanId { get; set; }
    public int CentralKitchenId { get; set; }
    public DateOnly PlanDate { get; set; }
    public string ExistingStatus { get; set; } = string.Empty;
}