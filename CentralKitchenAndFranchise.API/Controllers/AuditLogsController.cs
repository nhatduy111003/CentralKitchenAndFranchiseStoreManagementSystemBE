using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.AuditLogs;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.AuditLogs;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Roles = RoleNames.Admin)]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _service;

    public AuditLogsController(IAuditLogService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogResponse>>>> Search([FromQuery] AuditLogListQuery query, CancellationToken ct)
    {
        var data = await _service.SearchAsync(query, ct);
        return Ok(ApiResponse<PagedResult<AuditLogResponse>>.Ok(data));
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportCsv([FromQuery] AuditLogListQuery query, CancellationToken ct)
    {
        var bytes = await _service.ExportCsvAsync(query, ct);
        var filename = $"audit_logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}_utc.csv";
        return File(bytes, "text/csv", filename);
    }
}