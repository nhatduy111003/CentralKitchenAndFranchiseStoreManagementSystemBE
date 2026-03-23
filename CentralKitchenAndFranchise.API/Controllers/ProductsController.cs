using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Products;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff},{RoleNames.StoreStaff}")]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductResponse>>>> Search(
    [FromQuery] ProductListQuery query,
    CancellationToken ct)
    {
        var data = await _service.SearchAsync(query, ct);
        return Ok(ApiResponse<PagedResult<ProductResponse>>.Ok(data));
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff},{RoleNames.StoreStaff}")]
    public async Task<ActionResult<ApiResponse<ProductResponse>>> GetById(int id, CancellationToken ct)
    {
        var data = await _service.GetByIdAsync(id, ct);
        return Ok(ApiResponse<ProductResponse>.Ok(data));
    }
}
