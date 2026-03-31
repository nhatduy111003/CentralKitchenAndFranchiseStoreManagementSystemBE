using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Requests.StoreOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/supply/orders")]
[Authorize]
public class SupplyOrdersController : ControllerBase
{
    private readonly ISupplyOrderService _service;

    public SupplyOrdersController(ISupplyOrderService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetQueue(CancellationToken ct)
    {
        var result = await _service.GetQueueAsync(ct);
        return Ok(result);
    }

    // Return processed supply orders that already ended their lifecycle. DELIVERED/CONFIRMED/CANCELLED
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] SupplyOrderListQuery query,
        CancellationToken ct)
    {
        var result = await _service.GetHistoryAsync(query, ct);
        return Ok(result);
    }

    [HttpPatch("{orderId:int}/prepare-delivery")]
    public async Task<IActionResult> PrepareDelivery(
        int orderId,
        [FromBody] PrepareDeliveryRequest request,
        CancellationToken ct)
    {
        var result = await _service.PrepareDeliveryAsync(orderId, request, ct);
        return Ok(result);
    }

    [HttpPatch("{orderId:int}/delivery-status")]
    public async Task<IActionResult> UpdateDeliveryStatus(
        int orderId,
        [FromBody] UpdateSupplyDeliveryStatusRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateDeliveryStatusAsync(orderId, request, ct);
        return Ok(result);
    }
}