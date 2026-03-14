using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Ingredients;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Inventory;

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

        Task<PagedResult<StoreIngredientInventoryResponse>> GetStoreIngredientInventoryAsync(
            int franchiseId,
            InventoryListQuery query,
            CancellationToken ct = default);

        Task<PagedResult<StoreProductInventoryResponse>> GetStoreProductInventoryAsync(
           int franchiseId,
           InventoryListQuery query,
           CancellationToken ct = default);

        Task<PagedResult<IngredientInventoryHistoryResponse>> GetStoreIngredientHistoryAsync(
            int franchiseId,
            InventoryHistoryQuery query,
            CancellationToken ct = default);

        Task<PagedResult<ProductInventoryHistoryResponse>> GetStoreProductHistoryAsync(
            int franchiseId,
            InventoryHistoryQuery query,
            CancellationToken ct = default);

        Task<IngredientWasteResponse> CreateIngredientWasteAsync(
            int franchiseId,
            CreateIngredientWasteDto request,
            CancellationToken ct = default);
    }
}