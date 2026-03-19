using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Receivings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/franchises/{franchiseId:int}/receivings")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
public class FranchiseReceivingsController : ControllerBase
{
    private readonly IReceivingService _service;

    public FranchiseReceivingsController(IReceivingService service)
    {
        _service = service;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(int franchiseId, CancellationToken ct)
    {
        var result = await _service.GetPendingAsync(franchiseId, ct);
        return Ok(result);
    }

    [HttpGet("{deliveryId:int}")]
    public async Task<IActionResult> GetDetail(int franchiseId, int deliveryId, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(franchiseId, deliveryId, ct);
        return Ok(result);
    }

    [HttpPost("{deliveryId:int}/confirm")]
    public async Task<IActionResult> Confirm(
        int franchiseId,
        int deliveryId,
        [FromBody] ConfirmReceivingRequest request,
        CancellationToken ct)
    {
        var result = await _service.ConfirmAsync(franchiseId, deliveryId, request, ct);
        return Ok(result);
    }
}