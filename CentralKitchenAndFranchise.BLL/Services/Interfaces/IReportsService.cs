using CentralKitchenAndFranchise.DTO.Requests.Reports;
using CentralKitchenAndFranchise.DTO.Responses.Reports;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IReportsService
{
    Task<InventoryReportResponse> GetInventoryReportAsync(InventoryReportQuery query, CancellationToken ct = default);
    Task<WastageReportResponse> GetWastageReportAsync(WastageReportQuery query, CancellationToken ct = default);
    Task<StorePerformanceReportResponse> GetStorePerformanceReportAsync(StorePerformanceReportQuery query, CancellationToken ct = default);
}
