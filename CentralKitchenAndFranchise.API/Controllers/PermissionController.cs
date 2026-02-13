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
[Route("api/admin/permissions")]
[Authorize(Roles = RoleNames.Admin)]
public class PermissionController : ControllerBase
{
    private readonly IPermissionService _service;

    public PermissionController(IPermissionService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PermissionResponse>>>> GetList([FromQuery] PermissionListQuery query, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<PermissionResponse>>.Ok(await _service.SearchAsync(query, ct)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<PermissionResponse>>> GetById([FromRoute] int id, CancellationToken ct)
        => Ok(ApiResponse<PermissionResponse>.Ok(await _service.GetByIdAsync(id, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PermissionResponse>>> Create([FromBody] CreatePermissionDto dto, CancellationToken ct)
        => Ok(ApiResponse<PermissionResponse>.Ok(await _service.CreateAsync(dto, ct)));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<PermissionResponse>>> Update([FromRoute] int id, [FromBody] CreatePermissionDto dto, CancellationToken ct)
        => Ok(ApiResponse<PermissionResponse>.Ok(await _service.UpdateAsync(id, dto, ct)));

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<PermissionResponse>>> ChangeStatus([FromRoute] int id, [FromBody] ChangeEntityStatusRequest request, CancellationToken ct)
        => Ok(ApiResponse<PermissionResponse>.Ok(await _service.ChangeStatusAsync(id, request, ct)));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Delete([FromRoute] int id, [FromQuery] string? reason, CancellationToken ct)
    {
        await _service.DeleteAsync(id, reason, ct);
        return Ok(ApiResponse.Ok("Deactivated"));
    }
}