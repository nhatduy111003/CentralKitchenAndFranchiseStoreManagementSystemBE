using CentralKitchenAndFranchise.DTO.Requests.ProductionPlans;
using CentralKitchenAndFranchise.DTO.Responses.ProductionPlans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces
{
    public interface IProductionPlanService
    {
        Task<ProductionPlanResponse> CreateAsync(int franchiseId, CreateProductionPlanDto request, CancellationToken ct = default);
        Task<ProductionPlanResponse> UpdateStatusAsync(int franchiseId, int productionPlanId, UpdateProductionPlanStatusDto request, CancellationToken ct = default);
        Task<ProductionPlanResponse> GetByIdAsync(int franchiseId, int productionPlanId, CancellationToken ct = default);
    }
}
