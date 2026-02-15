using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Dashboard;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = RoleNames.Admin)]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _service;

    public AdminDashboardController(IAdminDashboardService service) => _service = service;

    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<AdminDashboardOverviewResponse>>> GetOverview(
        [FromQuery] AdminDashboardOverviewQuery query,
        CancellationToken ct)
        => Ok(ApiResponse<AdminDashboardOverviewResponse>.Ok(await _service.GetOverviewAsync(query, ct)));
}