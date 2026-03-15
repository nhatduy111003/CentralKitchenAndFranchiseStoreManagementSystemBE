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

    public IncomingOrdersController(IStoreOrderService service)
    {
        _service = service;
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
    public async Task<ActionResult<ApiResponse<IncomingOrderResponse>>> GetById(
        int centralKitchenId,
        int orderId,
        CancellationToken ct)
    {
        var data = await _service.GetIncomingByIdAsync(centralKitchenId, orderId, ct);
        return Ok(ApiResponse<IncomingOrderResponse>.Ok(data));
    }
}