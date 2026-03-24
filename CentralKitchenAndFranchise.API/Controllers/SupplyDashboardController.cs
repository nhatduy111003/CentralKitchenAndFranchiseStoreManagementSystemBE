using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/supply/dashboard")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.SupplyCoordinator}")]
public class SupplyDashboardController : ControllerBase
{
    private readonly ISupplyDashboardService _service;

    public SupplyDashboardController(ISupplyDashboardService service) => _service = service;

    /// <summary>Return the operational dashboard for supply preparation and delivery tracking.</summary>
    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<SupplyDashboardOverviewResponse>>> GetOverview(
        [FromQuery] SupplyDashboardOverviewQuery query,
        CancellationToken ct)
        => Ok(ApiResponse<SupplyDashboardOverviewResponse>.Ok(await _service.GetOverviewAsync(query, ct)));
}