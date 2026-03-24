using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/kitchen/dashboard")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
public class KitchenDashboardController : ControllerBase
{
    private readonly IKitchenDashboardService _service;

    public KitchenDashboardController(IKitchenDashboardService service) => _service = service;

    /// <summary>Return the operational dashboard for the selected or assigned central kitchen.</summary>
    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<KitchenDashboardOverviewResponse>>> GetOverview(
        [FromQuery] KitchenDashboardOverviewQuery query,
        CancellationToken ct)
        => Ok(ApiResponse<KitchenDashboardOverviewResponse>.Ok(await _service.GetOverviewAsync(query, ct)));
}