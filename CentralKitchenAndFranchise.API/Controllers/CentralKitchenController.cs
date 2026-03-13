using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.CentralKitchens;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.CentralKitchens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/admin/central-kitchens")]
public class CentralKitchenController : ControllerBase
{
    private readonly ICentralKitchenService _service;

    public CentralKitchenController(ICentralKitchenService service)
    {
        _service = service;
    }

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<CentralKitchenResponseDto>>>> GetAll()
        => Ok(ApiResponse<List<CentralKitchenResponseDto>>.Ok(await _service.GetAllAsync()));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<CentralKitchenResponseDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null
            ? NotFound(ApiResponse.Fail($"CentralKitchen {id} not found.", errorCode: "NOT_FOUND"))
            : Ok(ApiResponse<CentralKitchenResponseDto>.Ok(result));
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    public async Task<IActionResult> Create(CentralKitchenCreateDto dto)
    {
        var id = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id }, null);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CentralKitchenUpdateDto dto)
    {
        var success = await _service.UpdateAsync(id, dto);
        return success
            ? NoContent()
            : NotFound(ApiResponse.Fail($"CentralKitchen {id} not found.", errorCode: "NOT_FOUND"));
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);
        return success
            ? NoContent()
            : NotFound(ApiResponse.Fail($"CentralKitchen {id} not found.", errorCode: "NOT_FOUND"));
    }
}