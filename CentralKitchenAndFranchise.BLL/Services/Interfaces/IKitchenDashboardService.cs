using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IKitchenDashboardService
{
    /// <summary>Build the central-kitchen dashboard overview for kitchen operations.</summary>
    Task<KitchenDashboardOverviewResponse> GetOverviewAsync(KitchenDashboardOverviewQuery query, CancellationToken ct = default);
}