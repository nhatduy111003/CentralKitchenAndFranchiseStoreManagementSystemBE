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
[Route("api/central-kitchens/{centralKitchenId:int}/incoming-orders")]
public class IncomingOrdersController : ControllerBase
{
    private readonly IStoreOrderService _service;
    private readonly IKitchenOrderService _kitchenOrderService;

    public IncomingOrdersController(IStoreOrderService service, IKitchenOrderService kitchenOrderService)
    {
        _service = service;
        _kitchenOrderService = kitchenOrderService;
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
    public async Task<ActionResult<ApiResponse<PagedResult<IncomingOrderResponse>>>> Search(
        int centralKitchenId,
        [FromQuery] StoreOrderListQuery query,
        CancellationToken ct)
    {
        var data = await _service.SearchIncomingAsync(centralKitchenId, query, ct);
        return Ok(ApiResponse<PagedResult<IncomingOrderResponse>>.Ok(data));
    }


    [HttpGet("{orderId:int}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
    public async Task<IActionResult> GetDetail(int centralKitchenId, int orderId, CancellationToken ct)
    {
        var result = await _kitchenOrderService.GetDetailAsync(centralKitchenId, orderId, ct);
        return Ok(result);
    }

    [HttpPatch("{orderId:int}/receive")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
    public async Task<IActionResult> Receive(
        int centralKitchenId,
        int orderId,
        [FromBody] ReceiveIncomingOrderRequest request,
        CancellationToken ct)
    {
        var result = await _kitchenOrderService.ReceiveAsync(centralKitchenId, orderId, request, ct);
        return Ok(result);
    }

    [HttpPatch("{orderId:int}/processing-note")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
    public async Task<IActionResult> UpdateProcessingNote(
        int centralKitchenId,
        int orderId,
        [FromBody] UpdateProcessingNoteRequest request,
        CancellationToken ct)
    {
        var result = await _kitchenOrderService.UpdateProcessingNoteAsync(centralKitchenId, orderId, request, ct);
        return Ok(result);
    }

    [HttpPatch("{orderId:int}/forward-to-supply")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
    public async Task<IActionResult> ForwardToSupply(
        int centralKitchenId,
        int orderId,
        [FromBody] ForwardToSupplyRequest request,
        CancellationToken ct)
    {
        var result = await _kitchenOrderService.ForwardToSupplyAsync(centralKitchenId, orderId, request, ct);
        return Ok(result);
    }

    [HttpGet("{orderId:int}/history")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
    public async Task<IActionResult> GetHistory(int centralKitchenId, int orderId, CancellationToken ct)
    {
        var result = await _kitchenOrderService.GetHistoryAsync(centralKitchenId, orderId, ct);
        return Ok(result);
    }
}