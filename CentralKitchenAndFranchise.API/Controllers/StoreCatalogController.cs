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
[Route("api/franchises/{franchiseId:int}/catalog")]
public class StoreCatalogController : ControllerBase
{
    private readonly IStoreCatalogService _service;

    public StoreCatalogController(IStoreCatalogService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    public async Task<ActionResult<ApiResponse<PagedResult<StoreCatalogResponse>>>> GetCatalog(
        int franchiseId,
        [FromQuery] FranchiseCatalogListQuery query,
        CancellationToken ct)
    {
        var svcQuery = new StoreCatalogListQuery
        {
            FranchiseId = franchiseId,
            Status = query.Status,
            Q = query.Q,
            ProductType = query.ProductType,
            MinPrice = query.MinPrice,
            MaxPrice = query.MaxPrice,
            Page = query.Page,
            PageSize = query.PageSize,
            SortBy = query.SortBy,
            SortDir = query.SortDir
        };

        var data = await _service.SearchAsync(svcQuery, ct);
        return Ok(ApiResponse<PagedResult<StoreCatalogResponse>>.Ok(data));
    }

    [HttpPost]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    public async Task<ActionResult<ApiResponse<StoreCatalogResponse>>> Assign(
        int franchiseId,
        [FromBody] AssignCatalogProductRequest request,
        CancellationToken ct)
    {
        var svcReq = new UpsertStoreCatalogRequest
        {
            FranchiseId = franchiseId,
            ProductId = request.ProductId,
            Price = request.Price
        };

        var data = await _service.AssignAsync(svcReq, ct);
        return Ok(ApiResponse<StoreCatalogResponse>.Ok(data));
    }

    [HttpPut("{productId:int}/price")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    public async Task<ActionResult<ApiResponse<StoreCatalogResponse>>> UpdatePrice(
        int franchiseId,
        int productId,
        [FromBody] UpdateCatalogPriceRequest request,
        CancellationToken ct)
    {
        var svcReq = new UpdateStoreCatalogRequest { Price = request.Price };
        var data = await _service.UpdateAsync(franchiseId, productId, svcReq, ct);
        return Ok(ApiResponse<StoreCatalogResponse>.Ok(data));
    }

    [HttpPatch("{productId:int}/status")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    public async Task<ActionResult<ApiResponse<StoreCatalogResponse>>> ChangeStatus(
        int franchiseId,
        int productId,
        [FromBody] UpdateCatalogStatusRequest request,
        CancellationToken ct)
    {
        var svcReq = new ChangeStoreCatalogStatusRequest
        {
            Status = request.Status,
            Reason = request.Reason
        };

        var data = await _service.ChangeStatusAsync(franchiseId, productId, svcReq, ct);
        return Ok(ApiResponse<StoreCatalogResponse>.Ok(data));
    }

    [HttpDelete("{productId:int}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    public async Task<ActionResult<ApiResponse>> Delete(
        int franchiseId,
        int productId,
        CancellationToken ct)
    {
        await _service.DeleteAsync(franchiseId, productId, ct);
        return Ok(ApiResponse.Ok("Store catalog mapping deactivated."));
    }
}
