using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.StoreCatalog;
using CentralKitchenAndFranchise.DTO.Requests.StoreCatalogs;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.StoreCatalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/store-catalog")]
public class StoreCatalogController : ControllerBase
{
    private readonly IStoreCatalogService _service;

    public StoreCatalogController(IStoreCatalogService service)
    {
        _service = service;
    }

    /// <summary>
    /// List/search store catalog mappings for a specific franchise
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    public async Task<ActionResult<ApiResponse<PagedResult<StoreCatalogResponse>>>> Search(
        [FromQuery] StoreCatalogListQuery query,
        CancellationToken ct)
    {
        var data = await _service.SearchAsync(query, ct);
        return Ok(ApiResponse<PagedResult<StoreCatalogResponse>>.Ok(data));
    }

    /// <summary>
    /// Get a mapping by composite key (franchiseId, productId)
    /// </summary>
    [HttpGet("{franchiseId:int}/{productId:int}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    public async Task<ActionResult<ApiResponse<StoreCatalogResponse>>> GetByKey(int franchiseId, int productId, CancellationToken ct)
    {
        var data = await _service.GetByKeyAsync(franchiseId, productId, ct);
        return Ok(ApiResponse<StoreCatalogResponse>.Ok(data));
    }

    /// <summary>
    /// Assign (or reactivate) a product into a franchise store catalog
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    public async Task<ActionResult<ApiResponse<StoreCatalogResponse>>> Assign(
        [FromBody] UpsertStoreCatalogRequest request,
        CancellationToken ct)
    {
        var data = await _service.AssignAsync(request, ct);
        return Ok(ApiResponse<StoreCatalogResponse>.Ok(data));
    }

    /// <summary>
    /// Update mapping (price)
    /// </summary>
    [HttpPut("{franchiseId:int}/{productId:int}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    public async Task<ActionResult<ApiResponse<StoreCatalogResponse>>> Update(
        int franchiseId,
        int productId,
        [FromBody] UpdateStoreCatalogRequest request,
        CancellationToken ct)
    {
        var data = await _service.UpdateAsync(franchiseId, productId, request, ct);
        return Ok(ApiResponse<StoreCatalogResponse>.Ok(data));
    }

    /// <summary>
    /// Change mapping status (ACTIVE/INACTIVE)
    /// </summary>
    [HttpPatch("{franchiseId:int}/{productId:int}/status")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    public async Task<ActionResult<ApiResponse<StoreCatalogResponse>>> ChangeStatus(
        int franchiseId,
        int productId,
        [FromBody] ChangeStoreCatalogStatusRequest request,
        CancellationToken ct)
    {
        var data = await _service.ChangeStatusAsync(franchiseId, productId, request, ct);
        return Ok(ApiResponse<StoreCatalogResponse>.Ok(data));
    }

    /// <summary>
    /// Soft delete = set status to INACTIVE
    /// </summary>
    [HttpDelete("{franchiseId:int}/{productId:int}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    public async Task<ActionResult<ApiResponse>> Delete(int franchiseId, int productId, CancellationToken ct)
    {
        await _service.DeleteAsync(franchiseId, productId, ct);
        return Ok(ApiResponse.Ok("Store catalog mapping deactivated."));
    }
}
