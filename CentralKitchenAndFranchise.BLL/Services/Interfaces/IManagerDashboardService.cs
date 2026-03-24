using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IManagerDashboardService
{
    /// <summary>Build the global manager overview dashboard.</summary>
    Task<ManagerDashboardOverviewResponse> GetOverviewAsync(ManagerDashboardOverviewQuery query, CancellationToken ct = default);

    /// <summary>Build the manager dashboard for a single franchise.</summary>
    Task<ManagerDashboardOverviewResponse> GetFranchiseOverviewAsync(int franchiseId, ManagerDashboardOverviewQuery query, CancellationToken ct = default);
}