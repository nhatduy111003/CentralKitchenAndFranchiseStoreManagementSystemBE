using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.StoreOrders;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.StoreOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/franchises/{franchiseId:int}/store-orders")]
public class StoreOrdersController : ControllerBase
{
    private readonly IStoreOrderService _service;

    public StoreOrdersController(IStoreOrderService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
    public async Task<ActionResult<ApiResponse<PagedResult<StoreOrderResponse>>>> Search(
        int franchiseId,
        [FromQuery] StoreOrderListQuery query,
        CancellationToken ct)
    {
        var data = await _service.SearchAsync(franchiseId, query, ct);
        return Ok(ApiResponse<PagedResult<StoreOrderResponse>>.Ok(data));
    }

    [HttpGet("{orderId:int}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff},{RoleNames.SupplyCoordinator}")]
    public async Task<ActionResult<ApiResponse<StoreOrderResponse>>> GetById(
        int franchiseId,
        int orderId,
        CancellationToken ct)
    {
        var data = await _service.GetByIdAsync(franchiseId, orderId, ct);
        return Ok(ApiResponse<StoreOrderResponse>.Ok(data));
    }

    [HttpPost]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
    public async Task<ActionResult<ApiResponse<StoreOrderResponse>>> Create(
        int franchiseId,
        [FromBody] CreateStoreOrderRequest request,
        CancellationToken ct)
    {
        var data = await _service.CreateAsync(franchiseId, request, ct);
        return Ok(ApiResponse<StoreOrderResponse>.Ok(data));
    }

    [HttpPut("{orderId:int}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
    public async Task<ActionResult<ApiResponse<StoreOrderResponse>>> Update(
        int franchiseId,
        int orderId,
        [FromBody] UpdateStoreOrderRequest request,
        CancellationToken ct)
    {
        var data = await _service.UpdateAsync(franchiseId, orderId, request, ct);
        return Ok(ApiResponse<StoreOrderResponse>.Ok(data));
    }

    [HttpPost("{orderId:int}/submit")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
    public async Task<ActionResult<ApiResponse<StoreOrderResponse>>> Submit(
        int franchiseId,
        int orderId,
        CancellationToken ct)
    {
        var data = await _service.SubmitAsync(franchiseId, orderId, ct);
        return Ok(ApiResponse<StoreOrderResponse>.Ok(data));
    }

    [HttpPost("{orderId:int}/lock")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
    public async Task<ActionResult<ApiResponse<StoreOrderResponse>>> Lock(
        int franchiseId,
        int orderId,
        CancellationToken ct)
    {
        var data = await _service.LockAsync(franchiseId, orderId, ct);
        return Ok(ApiResponse<StoreOrderResponse>.Ok(data));
    }

    [HttpPost("{orderId:int}/cancel")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
    public async Task<ActionResult<ApiResponse<StoreOrderResponse>>> Cancel(
        int franchiseId,
        int orderId,
        [FromBody] CancelStoreOrderRequest request,
        CancellationToken ct)
    {
        var data = await _service.CancelAsync(franchiseId, orderId, request?.Reason, ct);
        return Ok(ApiResponse<StoreOrderResponse>.Ok(data));
    }
}