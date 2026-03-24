using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IStoreDashboardService
{
    /// <summary>Build the franchise dashboard overview for store operations.</summary>
    Task<StoreDashboardOverviewResponse> GetOverviewAsync(StoreDashboardOverviewQuery query, CancellationToken ct = default);
}