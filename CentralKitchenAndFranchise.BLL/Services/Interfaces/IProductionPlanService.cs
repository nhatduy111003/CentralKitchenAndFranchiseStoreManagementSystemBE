using CentralKitchenAndFranchise.DTO.Requests.ProductionPlans;
using CentralKitchenAndFranchise.DTO.Responses.ProductionPlans;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces
{
    public interface IProductionPlanService
    {
        Task<ProductionPlanResponse> CreateAsync(int ckId, CreateProductionPlanDto request, CancellationToken ct = default);
        Task<ProductionPlanResponse> UpdateStatusAsync(int ckId, int productionPlanId, UpdateProductionPlanStatusDto request, CancellationToken ct = default);
        Task<ProductionPlanResponse> GetByIdAsync(int ckId, int productionPlanId, CancellationToken ct = default);
        Task<ProductionPlanResponse> GetByDateAsync(int ckId, DateOnly planDate, CancellationToken ct = default);
    }
}