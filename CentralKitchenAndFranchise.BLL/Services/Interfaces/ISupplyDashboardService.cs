using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface ISupplyDashboardService
{
    /// <summary>Build the supply dashboard overview for shipment preparation and delivery follow-up.</summary>
    Task<SupplyDashboardOverviewResponse> GetOverviewAsync(SupplyDashboardOverviewQuery query, CancellationToken ct = default);
}