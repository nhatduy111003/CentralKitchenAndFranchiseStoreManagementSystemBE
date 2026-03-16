using CentralKitchenAndFranchise.DTO.Responses.ProductionPlans;

namespace CentralKitchenAndFranchise.BLL.Exceptions;

public sealed class ProductionPlanConflictException : Exception
{
    public ProductionPlanConflictException(
        string message,
        int centralKitchenId,
        DateOnly planDate,
        int existingProductionPlanId,
        string existingStatus) : base(message)
    {
        Payload = new ProductionPlanConflictData
        {
            CentralKitchenId = centralKitchenId,
            PlanDate = planDate,
            ExistingProductionPlanId = existingProductionPlanId,
            ExistingStatus = existingStatus
        };
    }

    public ProductionPlanConflictData Payload { get; }
}