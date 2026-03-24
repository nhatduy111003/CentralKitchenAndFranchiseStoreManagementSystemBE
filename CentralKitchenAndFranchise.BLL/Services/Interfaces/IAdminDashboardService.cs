using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IAdminDashboardService
{
    /// Build the admin overview dashboard.
    Task<AdminDashboardOverviewResponse> GetOverviewAsync(AdminDashboardOverviewQuery query, CancellationToken ct = default);
}