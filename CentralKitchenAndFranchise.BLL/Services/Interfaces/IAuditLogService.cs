using CentralKitchenAndFranchise.DTO.Requests.AuditLogs;
using CentralKitchenAndFranchise.DTO.Responses.AuditLogs;
using CentralKitchenAndFranchise.DTO.Responses.Common;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IAuditLogService
{
    Task<PagedResult<AuditLogResponse>> SearchAsync(AuditLogListQuery query, CancellationToken ct = default);

    /// Export result as CSV (UTF-8 with BOM) for Excel-friendly open.
    Task<byte[]> ExportCsvAsync(AuditLogListQuery query, CancellationToken ct = default);
}