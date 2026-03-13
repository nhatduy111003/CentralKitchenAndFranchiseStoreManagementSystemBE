using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Responses;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces
{
    public interface IInventoryService
    {
        Task<IngredientInboundResponse> InboundIngredientAsync(
            int franchiseId,
            CreateIngredientInboundDto request,
            CancellationToken ct = default);

        Task<AdjustIngredientInventoryResponse> AdjustIngredientAsync(
            int franchiseId,
            AdjustIngredientInventoryDto request,
            CancellationToken ct = default);

        Task<ProductInboundResponse> InboundProductAsync(
            int franchiseId,
            CreateProductInboundDto request,
            CancellationToken ct = default);

        Task<IssueIngredientsByProductionPlanResponse> IssueIngredientsByProductionPlanAsync(
            int centralKitchenId,
            int productionPlanId,
            IssueIngredientsByProductionPlanDto request,
            CancellationToken ct = default);
    }
}