using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Ingredients;
using CentralKitchenAndFranchise.DTO.Requests.Inventory;
using CentralKitchenAndFranchise.DTO.Responses;
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

        Task<FranchiseInventorySummaryResponse> GetFranchiseInventorySummaryAsync(
            int franchiseId,
            CancellationToken ct = default);

        Task<CentralKitchenInventorySummaryResponse> GetCentralKitchenInventorySummaryAsync(
            int centralKitchenId,
            CancellationToken ct = default);
        //CRUD Ingredient/ProductBatch cho CentralKitchen
        Task<CentralKitchenIngredientBatchResponse> InboundCentralKitchenIngredientAsync(
            int centralKitchenId,
            CreateIngredientBatchDto request,
            CancellationToken ct = default);

        Task<AdjustIngredientInventoryResponse> AdjustCentralKitchenIngredientAsync(
            int centralKitchenId,
            AdjustIngredientInventoryDto request,
            CancellationToken ct = default);

        Task<List<CentralKitchenIngredientBatchResponse>> GetCentralKitchenIngredientBatchesAsync(
            int centralKitchenId,
            int? ingredientId = null,
            bool includeZero = false,
            CancellationToken ct = default);

        Task<CentralKitchenIngredientBatchResponse> GetCentralKitchenIngredientBatchByIdAsync(
            int centralKitchenId,
            int batchId,
            CancellationToken ct = default);

        Task<CentralKitchenIngredientBatchResponse> UpdateCentralKitchenIngredientBatchCodeAsync(
            int centralKitchenId,
            int batchId,
            UpdateBatchCodeRequest request,
            CancellationToken ct = default);

        Task DeleteCentralKitchenIngredientBatchAsync(
            int centralKitchenId,
            int batchId,
            CancellationToken ct = default);

        Task<CentralKitchenProductBatchResponse> InboundCentralKitchenProductAsync(
            int centralKitchenId,
            CreateCentralKitchenProductBatchDto request,
            CancellationToken ct = default);

        Task<AdjustProductInventoryResponse> AdjustCentralKitchenProductAsync(
            int centralKitchenId,
            AdjustProductInventoryDto request,
            CancellationToken ct = default);

        Task<List<CentralKitchenProductBatchResponse>> GetCentralKitchenProductBatchesAsync(
            int centralKitchenId,
            int? productId = null,
            bool includeZero = false,
            CancellationToken ct = default);

        Task<CentralKitchenProductBatchResponse> GetCentralKitchenProductBatchByIdAsync(
            int centralKitchenId,
            int batchId,
            CancellationToken ct = default);

        Task<CentralKitchenProductBatchResponse> UpdateCentralKitchenProductBatchCodeAsync(
            int centralKitchenId,
            int batchId,
            UpdateBatchCodeRequest request,
            CancellationToken ct = default);

        Task DeleteCentralKitchenProductBatchAsync(
            int centralKitchenId,
            int batchId,
            CancellationToken ct = default);
    }
}