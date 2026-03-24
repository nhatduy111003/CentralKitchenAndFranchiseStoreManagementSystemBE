using CentralKitchenAndFranchise.DTO.Responses.Dashboard;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IDashboardScopeService
{
    /// <summary>Resolve the effective central-kitchen scope for dashboard requests.</summary>
    Task<DashboardCentralKitchenScope> ResolveCentralKitchenScopeAsync(int? requestedCentralKitchenId, CancellationToken ct = default);

    /// <summary>Resolve the effective franchise scope for dashboard requests.</summary>
    Task<DashboardFranchiseScope> ResolveFranchiseScopeAsync(int? requestedFranchiseId, CancellationToken ct = default);

    /// <summary>Return all active franchise ids for the selected central kitchen.</summary>
    Task<List<int>> GetActiveFranchiseIdsByCentralKitchenAsync(int centralKitchenId, CancellationToken ct = default);

    /// <summary>Return all active franchise ids in the system.</summary>
    Task<List<int>> GetAllActiveFranchiseIdsAsync(CancellationToken ct = default);
}