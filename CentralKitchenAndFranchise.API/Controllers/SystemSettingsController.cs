using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.SystemSettings;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.SystemSettings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/admin/system-settings")]
[Authorize(Roles = RoleNames.Admin)]
public class SystemSettingsController : ControllerBase
{
    private readonly ISystemSettingService _service;

    public SystemSettingsController(ISystemSettingService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SystemSettingResponse>>>> Search(
        [FromQuery] SystemSettingListQuery query,
        CancellationToken ct)
    {
        var data = await _service.SearchAsync(query, ct);
        return Ok(ApiResponse<PagedResult<SystemSettingResponse>>.Ok(data));
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<ApiResponse<SystemSettingResponse>>> GetByKey(string key, CancellationToken ct)
    {
        var data = await _service.GetByKeyAsync(key, ct);
        return Ok(ApiResponse<SystemSettingResponse>.Ok(data));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse>> Create([FromBody] SystemSettingRequest req, CancellationToken ct)
    {
        var id = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetByKey), new { key = req.Key }, ApiResponse.Ok($"Created (id={id})."));
    }

    [HttpPut("{key}")]
    public async Task<ActionResult<ApiResponse>> Update(string key, [FromBody] SystemSettingRequest req, CancellationToken ct)
    {
        await _service.UpdateAsync(key, req, ct);
        return Ok(ApiResponse.Ok("Updated."));
    }

    [HttpDelete("{key}")]
    public async Task<ActionResult<ApiResponse>> Delete(string key, CancellationToken ct)
    {
        await _service.DeleteAsync(key, ct);
        return Ok(ApiResponse.Ok("Deleted."));
    }
}