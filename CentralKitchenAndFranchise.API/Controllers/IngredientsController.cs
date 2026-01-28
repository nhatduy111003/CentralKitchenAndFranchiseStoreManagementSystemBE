using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Requests.Ingredients;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Ingredients;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/ingredients")]
public class IngredientsController : ControllerBase
{
    private readonly IIngredientService _service;

    public IngredientsController(IIngredientService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<IngredientResponse>>>> GetAll(CancellationToken ct)
        => Ok(ApiResponse<List<IngredientResponse>>.Ok(await _service.GetAllAsync(ct)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<IngredientResponse>>> GetById([FromRoute] int id, CancellationToken ct)
        => Ok(ApiResponse<IngredientResponse>.Ok(await _service.GetByIdAsync(id, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<IngredientResponse>>> Create([FromBody] CreateIngredientRequest req, CancellationToken ct)
        => Ok(ApiResponse<IngredientResponse>.Ok(await _service.CreateAsync(req, ct)));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<IngredientResponse>>> Update([FromRoute] int id, [FromBody] UpdateIngredientRequest req, CancellationToken ct)
        => Ok(ApiResponse<IngredientResponse>.Ok(await _service.UpdateAsync(id, req, ct)));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Delete([FromRoute] int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Deleted"));
    }
}
