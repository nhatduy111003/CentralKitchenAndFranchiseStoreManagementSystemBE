using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/store/dashboard")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
public class StoreDashboardController : ControllerBase
{
    private readonly IStoreDashboardService _service;

    public StoreDashboardController(IStoreDashboardService service) => _service = service;

    /// <summary>Return the operational dashboard for the selected or assigned franchise.</summary>
    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<StoreDashboardOverviewResponse>>> GetOverview(
        [FromQuery] StoreDashboardOverviewQuery query,
        CancellationToken ct)
        => Ok(ApiResponse<StoreDashboardOverviewResponse>.Ok(await _service.GetOverviewAsync(query, ct)));
}