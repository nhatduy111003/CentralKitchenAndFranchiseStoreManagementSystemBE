// CentralKitchenAndFranchise.BLL/Services/Interfaces/IManagerDashboardService.cs
using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IManagerDashboardService
{
    Task<ManagerDashboardOverviewResponse> GetOverviewAsync(ManagerDashboardOverviewQuery query, CancellationToken ct = default);

    Task<ManagerDashboardOverviewResponse> GetFranchiseOverviewAsync(int franchiseId, ManagerDashboardOverviewQuery query, CancellationToken ct = default);
}