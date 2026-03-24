using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/manager")]
public class ManagerDashboardController : ControllerBase
{
    private readonly IManagerDashboardService _service;

    public ManagerDashboardController(IManagerDashboardService service) => _service = service;

    /// <summary>Return the global manager overview dashboard.</summary>
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpGet("dashboard/overview")]
    public async Task<ActionResult<ApiResponse<ManagerDashboardOverviewResponse>>> GetOverview(
    [FromQuery] ManagerDashboardOverviewQuery query,
    CancellationToken ct)
    => Ok(ApiResponse<ManagerDashboardOverviewResponse>.Ok(await _service.GetOverviewAsync(query, ct)));

    /// <summary>Return the manager dashboard for one franchise.</summary>
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpGet("franchises/{franchiseId:int}/dashboard/overview")]
    public async Task<ActionResult<ApiResponse<ManagerDashboardOverviewResponse>>> GetFranchiseOverview(
        [FromRoute] int franchiseId,
        [FromQuery] ManagerDashboardOverviewQuery query,
        CancellationToken ct)
        => Ok(ApiResponse<ManagerDashboardOverviewResponse>.Ok(await _service.GetFranchiseOverviewAsync(franchiseId, query, ct)));
}