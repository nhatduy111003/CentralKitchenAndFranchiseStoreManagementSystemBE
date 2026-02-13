using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Suppliers;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Suppliers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/suppliers")]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _service;

    public SuppliersController(ISupplierService service) => _service = service;

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SupplierResponse>>>> GetList(
        [FromQuery] SupplierListQuery query,
        CancellationToken ct)
        => Ok(ApiResponse<PagedResult<SupplierResponse>>.Ok(await _service.SearchAsync(query, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<SupplierResponse>>> GetById([FromRoute] int id, CancellationToken ct)
        => Ok(ApiResponse<SupplierResponse>.Ok(await _service.GetByIdAsync(id, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<SupplierResponse>>> Create([FromBody] CreateSupplierRequest req, CancellationToken ct)
        => Ok(ApiResponse<SupplierResponse>.Ok(await _service.CreateAsync(req, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<SupplierResponse>>> Update([FromRoute] int id, [FromBody] UpdateSupplierRequest req, CancellationToken ct)
        => Ok(ApiResponse<SupplierResponse>.Ok(await _service.UpdateAsync(id, req, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<SupplierResponse>>> ChangeStatus([FromRoute] int id, [FromBody] ChangeSupplierStatusRequest req, CancellationToken ct)
        => Ok(ApiResponse<SupplierResponse>.Ok(await _service.ChangeStatusAsync(id, req, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Delete([FromRoute] int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Deactivated"));
    }
}