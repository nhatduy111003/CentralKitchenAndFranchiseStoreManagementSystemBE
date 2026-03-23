using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Ingredients;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Ingredients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/ingredients")]
public class IngredientsController : ControllerBase
{
    private readonly IIngredientService _service;

    public IngredientsController(IIngredientService service) => _service = service;

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff},{RoleNames.StoreStaff}")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<IngredientResponse>>>> GetList(
    [FromQuery] IngredientListQuery query,
    CancellationToken ct)
    => Ok(ApiResponse<PagedResult<IngredientResponse>>.Ok(await _service.SearchAsync(query, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff},{RoleNames.StoreStaff}")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<IngredientResponse>>> GetById([FromRoute] int id, CancellationToken ct)
        => Ok(ApiResponse<IngredientResponse>.Ok(await _service.GetByIdAsync(id, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<IngredientResponse>>> Create([FromBody] CreateIngredientRequest req, CancellationToken ct)
        => Ok(ApiResponse<IngredientResponse>.Ok(await _service.CreateAsync(req, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<IngredientResponse>>> Update([FromRoute] int id, [FromBody] UpdateIngredientRequest req, CancellationToken ct)
        => Ok(ApiResponse<IngredientResponse>.Ok(await _service.UpdateAsync(id, req, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<IngredientResponse>>> ChangeStatus([FromRoute] int id, [FromBody] ChangeIngredientStatusRequest req, CancellationToken ct)
        => Ok(ApiResponse<IngredientResponse>.Ok(await _service.ChangeStatusAsync(id, req, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Delete([FromRoute] int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Deactivated"));
    }
}