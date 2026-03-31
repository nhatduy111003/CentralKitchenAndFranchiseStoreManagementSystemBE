using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.InventoryHistory;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.InventoryHistory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/franchises/{franchiseId:int}/inventory/history")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
public class FranchiseInventoryHistoryController : ControllerBase
{
    private readonly IInventoryHistoryService _service;

    public FranchiseInventoryHistoryController(IInventoryHistoryService service)
    {
        _service = service;
    }

    [HttpGet("movements")]
    public async Task<ActionResult<ApiResponse<PagedResult<InventoryHistoryMovementResponse>>>> GetMovements(
        int franchiseId,
        [FromQuery] InventoryHistoryMovementsQuery query,
        CancellationToken ct)
    {
        var data = await _service.GetFranchiseMovementsAsync(franchiseId, query, ct);
        return Ok(ApiResponse<PagedResult<InventoryHistoryMovementResponse>>.Ok(data));
    }

    [HttpGet("batches/{batchId:int}")]
    public async Task<ActionResult<ApiResponse<InventoryBatchLifecycleResponse>>> GetBatchLifecycle(
        int franchiseId,
        int batchId,
        [FromQuery] InventoryBatchLifecycleQuery query,
        CancellationToken ct)
    {
        var data = await _service.GetFranchiseBatchLifecycleAsync(franchiseId, batchId, query, ct);
        return Ok(ApiResponse<InventoryBatchLifecycleResponse>.Ok(data));
    }
}
