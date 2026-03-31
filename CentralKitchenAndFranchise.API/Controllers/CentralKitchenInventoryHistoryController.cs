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
[Route("api/central-kitchens/{centralKitchenId:int}/inventory/history")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff},{RoleNames.SupplyCoordinator}")]
public class CentralKitchenInventoryHistoryController : ControllerBase
{
    private readonly IInventoryHistoryService _service;

    public CentralKitchenInventoryHistoryController(IInventoryHistoryService service)
    {
        _service = service;
    }

    [HttpGet("movements")]
    public async Task<ActionResult<ApiResponse<PagedResult<InventoryHistoryMovementResponse>>>> GetMovements(
        int centralKitchenId,
        [FromQuery] InventoryHistoryMovementsQuery query,
        CancellationToken ct)
    {
        var data = await _service.GetCentralKitchenMovementsAsync(centralKitchenId, query, ct);
        return Ok(ApiResponse<PagedResult<InventoryHistoryMovementResponse>>.Ok(data));
    }

    [HttpGet("batches/{batchId:int}")]
    public async Task<ActionResult<ApiResponse<InventoryBatchLifecycleResponse>>> GetBatchLifecycle(
        int centralKitchenId,
        int batchId,
        [FromQuery] InventoryBatchLifecycleQuery query,
        CancellationToken ct)
    {
        var data = await _service.GetCentralKitchenBatchLifecycleAsync(centralKitchenId, batchId, query, ct);
        return Ok(ApiResponse<InventoryBatchLifecycleResponse>.Ok(data));
    }
}
