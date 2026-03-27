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
    private readonly IReportExportService _exportService;

    public ReportsController(
        IReportsService service,
        IReportExportService exportService)
    {
        _service = service;
        _exportService = exportService;
    }

    /// <summary>Return opening/inbound/outbound/waste/closing by item for one inventory scope.</summary>
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff},{RoleNames.KitchenStaff},{RoleNames.SupplyCoordinator}")]
    [HttpGet("inventory")]
    public async Task<ActionResult<ApiResponse<InventoryReportResponse>>> GetInventory(
        [FromQuery] InventoryReportQuery query,
        CancellationToken ct)
        => Ok(ApiResponse<InventoryReportResponse>.Ok(await _service.GetInventoryReportAsync(query, ct)));

    /// <summary>Return ingredient wastage aggregates for store, kitchen, or chain scope.</summary>
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff},{RoleNames.KitchenStaff},{RoleNames.SupplyCoordinator}")]
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

    /// <summary>Export one monthly XLSX workbook for a store/franchise scope.</summary>
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
    [HttpGet("export/store-monthly")]
    public async Task<IActionResult> ExportStoreMonthly(
        [FromQuery] StoreMonthlyExportQuery query,
        CancellationToken ct)
    {
        var file = await _exportService.ExportStoreMonthlyAsync(query, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Export one monthly XLSX workbook for a central-kitchen scope.</summary>
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff},{RoleNames.SupplyCoordinator}")]
    [HttpGet("export/kitchen-monthly")]
    public async Task<IActionResult> ExportKitchenMonthly(
        [FromQuery] KitchenMonthlyExportQuery query,
        CancellationToken ct)
    {
        var file = await _exportService.ExportKitchenMonthlyAsync(query, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
