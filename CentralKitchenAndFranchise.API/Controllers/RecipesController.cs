using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Recipes;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Recipes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/recipes")]
public class RecipesController : ControllerBase
{
    private readonly IRecipeService _service;
    public RecipesController(IRecipeService service) => _service = service;

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<RecipeResponse>>>> GetList([FromQuery] RecipeListQuery query, CancellationToken ct)
        => Ok(ApiResponse<PagedResult<RecipeResponse>>.Ok(await _service.SearchAsync(query, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<RecipeResponse>>> GetById([FromRoute] int id, CancellationToken ct)
        => Ok(ApiResponse<RecipeResponse>.Ok(await _service.GetByIdAsync(id, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<RecipeResponse>>> Create([FromBody] CreateRecipeRequest req, CancellationToken ct)
        => Ok(ApiResponse<RecipeResponse>.Ok(await _service.CreateAsync(req, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<RecipeResponse>>> Update([FromRoute] int id, [FromBody] UpdateRecipeRequest req, CancellationToken ct)
        => Ok(ApiResponse<RecipeResponse>.Ok(await _service.UpdateAsync(id, req, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<RecipeResponse>>> ChangeStatus([FromRoute] int id, [FromBody] ChangeRecipeStatusRequest req, CancellationToken ct)
        => Ok(ApiResponse<RecipeResponse>.Ok(await _service.ChangeStatusAsync(id, req, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Delete([FromRoute] int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Deactivated"));
    }
}