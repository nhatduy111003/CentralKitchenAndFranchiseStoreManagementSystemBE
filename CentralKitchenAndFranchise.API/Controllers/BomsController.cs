using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Boms;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Boms;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/boms")]
public class BomsController : ControllerBase
{
    private readonly IBomService _service;
    public BomsController(IBomService service) => _service = service;

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<BomResponse>>>> GetList([FromQuery] BomListQuery query, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<BomResponse>>.Ok(await _service.SearchAsync(query, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<BomResponse>>> GetById([FromRoute] int id, CancellationToken ct)
        => Ok(ApiResponse<BomResponse>.Ok(await _service.GetByIdAsync(id, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<BomResponse>>> Create([FromBody] CreateBomRequest req, CancellationToken ct)
        => Ok(ApiResponse<BomResponse>.Ok(await _service.CreateAsync(req, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<BomResponse>>> Update([FromRoute] int id, [FromBody] UpdateBomRequest req, CancellationToken ct)
        => Ok(ApiResponse<BomResponse>.Ok(await _service.UpdateAsync(id, req, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<BomResponse>>> ChangeStatus([FromRoute] int id, [FromBody] ChangeBomStatusRequest req, CancellationToken ct)
        => Ok(ApiResponse<BomResponse>.Ok(await _service.ChangeStatusAsync(id, req, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Delete([FromRoute] int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Deactivated"));
    }
}