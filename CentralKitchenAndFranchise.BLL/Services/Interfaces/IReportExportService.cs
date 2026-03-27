using CentralKitchenAndFranchise.DTO.Requests.Reports;
using CentralKitchenAndFranchise.DTO.Responses.Common;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IReportExportService
{
    Task<FileExportPayload> ExportStoreMonthlyAsync(StoreMonthlyExportQuery query, CancellationToken ct = default);
    Task<FileExportPayload> ExportKitchenMonthlyAsync(KitchenMonthlyExportQuery query, CancellationToken ct = default);
}