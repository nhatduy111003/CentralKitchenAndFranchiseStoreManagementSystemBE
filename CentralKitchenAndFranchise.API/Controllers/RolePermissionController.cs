using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Rbac;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Rbac;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/admin/roles/{roleId:int}/permissions")]
[Authorize(Roles = RoleNames.Admin)]
public class RolePermissionController : ControllerBase
{
    private readonly IRolePermissionService _service;

    public RolePermissionController(IRolePermissionService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<RolePermissionResponse>>>> Get([FromRoute] int roleId, CancellationToken ct)
        => Ok(ApiResponse<List<RolePermissionResponse>>.Ok(await _service.GetPermissionsByRoleAsync(roleId, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse>> Assign([FromRoute] int roleId, [FromBody] AssignPermissionToRoleRequest req, CancellationToken ct)
    {
        await _service.AssignToRoleAsync(roleId, req.PermissionId, ct);
        return Ok(ApiResponse.Ok("Assigned"));
    }

    [HttpDelete("{permissionId:int}")]
    public async Task<ActionResult<ApiResponse>> Remove([FromRoute] int roleId, [FromRoute] int permissionId, CancellationToken ct)
    {
        await _service.RemovePermissionAsync(roleId, permissionId, ct);
        return Ok(ApiResponse.Ok("Removed"));
    }
}