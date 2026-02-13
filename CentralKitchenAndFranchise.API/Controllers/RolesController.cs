using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Rbac;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Rbac;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/admin/roles")]
[Authorize(Roles = RoleNames.Admin)]
public class RolesController : ControllerBase
{
    private readonly IRoleService _service;

    public RolesController(IRoleService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<RoleResponse>>>> GetList([FromQuery] RoleListQuery query, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<RoleResponse>>.Ok(await _service.SearchAsync(query, ct)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<RoleResponse>>> GetById([FromRoute] int id, CancellationToken ct)
        => Ok(ApiResponse<RoleResponse>.Ok(await _service.GetByIdAsync(id, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<RoleResponse>>> Create([FromBody] RoleRequestDto dto, CancellationToken ct)
        => Ok(ApiResponse<RoleResponse>.Ok(await _service.CreateAsync(dto, ct)));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<RoleResponse>>> Update([FromRoute] int id, [FromBody] RoleRequestDto dto, CancellationToken ct)
        => Ok(ApiResponse<RoleResponse>.Ok(await _service.UpdateAsync(id, dto, ct)));

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<RoleResponse>>> ChangeStatus([FromRoute] int id, [FromBody] ChangeEntityStatusRequest request, CancellationToken ct)
        => Ok(ApiResponse<RoleResponse>.Ok(await _service.ChangeStatusAsync(id, request, ct)));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Delete([FromRoute] int id, [FromQuery] string? reason, CancellationToken ct)
    {
        await _service.DeleteAsync(id, reason, ct);
        return Ok(ApiResponse.Ok("Deactivated"));
    }
}