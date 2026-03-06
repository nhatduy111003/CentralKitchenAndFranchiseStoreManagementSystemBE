using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Deliveries;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Deliveries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
public class DeliveriesController : ControllerBase
{
    private readonly IDeliveryService _service;

    public DeliveriesController(IDeliveryService service) => _service = service;

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.SupplyCoordinator}")]
    [HttpPost("api/delivery-plans")]
    public async Task<ActionResult<ApiResponse<int>>> CreatePlan([FromBody] CreateDeliveryPlanRequest req, CancellationToken ct)
        => Ok(ApiResponse<int>.Ok(await _service.CreatePlanAsync(req, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.SupplyCoordinator}")]
    [HttpPost("api/deliveries")]
    public async Task<ActionResult<ApiResponse<int>>> CreateDelivery([FromBody] CreateDeliveryRequest req, CancellationToken ct)
        => Ok(ApiResponse<int>.Ok(await _service.CreateDeliveryAsync(req, ct)));

    [Authorize]
    [HttpGet("api/deliveries/{deliveryId:int}")]
    public async Task<ActionResult<ApiResponse<DeliveryDetailsResponse>>> GetById([FromRoute] int deliveryId, CancellationToken ct)
        => Ok(ApiResponse<DeliveryDetailsResponse>.Ok(await _service.GetByIdAsync(deliveryId, ct)));

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.SupplyCoordinator}")]
    [HttpPut("api/deliveries/{deliveryId:int}/product-items")]
    public async Task<ActionResult<ApiResponse>> UpsertProductItems([FromRoute] int deliveryId, [FromBody] List<UpsertDeliveryProductItemRequest> items, CancellationToken ct)
    {
        await _service.UpsertProductItemsAsync(deliveryId, items, ct);
        return Ok(ApiResponse.Ok("Saved"));
    }

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.SupplyCoordinator}")]
    [HttpPut("api/deliveries/{deliveryId:int}/ingredient-items")]
    public async Task<ActionResult<ApiResponse>> UpsertIngredientItems([FromRoute] int deliveryId, [FromBody] List<UpsertDeliveryIngredientItemRequest> items, CancellationToken ct)
    {
        await _service.UpsertIngredientItemsAsync(deliveryId, items, ct);
        return Ok(ApiResponse.Ok("Saved"));
    }

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [HttpPost("api/deliveries/{deliveryId:int}/confirm")]
    public async Task<ActionResult<ApiResponse>> Confirm([FromRoute] int deliveryId, CancellationToken ct)
    {
        await _service.ConfirmAsync(deliveryId, ct);
        return Ok(ApiResponse.Ok("Confirmed"));
    }
}
