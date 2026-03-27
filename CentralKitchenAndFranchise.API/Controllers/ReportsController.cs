using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Reports;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportsService _service;

    public ReportsController(IReportsService service) => _service = service;

    /// <summary>Return opening/inbound/outbound/waste/closing by item for one inventory scope.</summary>
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
    [HttpGet("inventory")]
    public async Task<ActionResult<ApiResponse<InventoryReportResponse>>> GetInventory(
        [FromQuery] InventoryReportQuery query,
        CancellationToken ct)
        => Ok(ApiResponse<InventoryReportResponse>.Ok(await _service.GetInventoryReportAsync(query, ct)));

    /// <summary>Return ingredient wastage aggregates for store, kitchen, or chain scope.</summary>
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
    [HttpGet("wastage")]
    public async Task<ActionResult<ApiResponse<WastageReportResponse>>> GetWastage(
        [FromQuery] WastageReportQuery query,
        CancellationToken ct)
        => Ok(ApiResponse<WastageReportResponse>.Ok(await _service.GetWastageReportAsync(query, ct)));

    /// <summary>Return store spending and delivery SLA metrics for manager/admin reporting.</summary>
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpGet("store-performance")]
    public async Task<ActionResult<ApiResponse<StorePerformanceReportResponse>>> GetStorePerformance(
        [FromQuery] StorePerformanceReportQuery query,
        CancellationToken ct)
        => Ok(ApiResponse<StorePerformanceReportResponse>.Ok(await _service.GetStorePerformanceReportAsync(query, ct)));
}
