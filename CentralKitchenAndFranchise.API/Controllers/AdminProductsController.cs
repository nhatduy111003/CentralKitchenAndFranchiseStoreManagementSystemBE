using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Products;
using CentralKitchenAndFranchise.DTO.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
public class AdminProductsController : ControllerBase
{
    private readonly IProductService _service;

    public AdminProductsController(IProductService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse>> Create([FromBody] ProductCreateRequest req, CancellationToken ct)
    {
        var id = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse.Ok($"Created product (id={id})."));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> GetById(int id, CancellationToken ct)
    {
        var data = await _service.GetByIdAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(data));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Update(int id, [FromBody] ProductUpdateRequest req, CancellationToken ct)
    {
        await _service.UpdateAsync(id, req, ct);
        return Ok(ApiResponse.Ok("Updated."));
    }

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ApiResponse>> ChangeStatus(int id, [FromBody] ProductStatusUpdateRequest req, CancellationToken ct)
    {
        await _service.ChangeStatusAsync(id, req, ct);
        return Ok(ApiResponse.Ok("Status updated."));
    }

    // Standard endpoint: DELETE /api/admin/products/123
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Deactivate(int id, CancellationToken ct)
    {
        await _service.DeactivateAsync(id, ct);
        return Ok(ApiResponse.Ok("Deactivated."));
    }

    // Compatibility endpoint: DELETE /api/admin/products?id=123
    [HttpDelete]
    public async Task<ActionResult<ApiResponse>> DeactivateByQuery([FromQuery] int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(ApiResponse.Fail("id must be > 0.", errorCode: "VALIDATION_ERROR"));
        await _service.DeactivateAsync(id, ct);
        return Ok(ApiResponse.Ok("Deactivated."));
    }
}