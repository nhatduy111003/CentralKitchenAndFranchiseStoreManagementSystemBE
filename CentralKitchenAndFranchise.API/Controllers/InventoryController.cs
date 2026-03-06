using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers
{
    [ApiController]
    [Route("api/franchises/{franchiseId:int}/inventory/ingredients")]
    public class IngredientInventoryController : ControllerBase
    {
        private readonly IInventoryService _service;

        public IngredientInventoryController(IInventoryService service)
        {
            _service = service;
        }

        // Central Kitchen nhập kho NVL
        [HttpPost("InboundIngredient")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<IngredientInboundResponse>>> Inbound(
            int franchiseId,
            [FromBody] CreateIngredientInboundDto request,
            CancellationToken ct)
        {
            var data = await _service.InboundIngredientAsync(franchiseId, request, ct);
            return Ok(ApiResponse<IngredientInboundResponse>.Ok(data));
        }

        [HttpPost("issue-by-production-plan/{productionPlanId:int}")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<IssueIngredientsByProductionPlanResponse>>> IssueByProductionPlan(
            int franchiseId,
            int productionPlanId,
            [FromBody] IssueIngredientsByProductionPlanDto request,
            CancellationToken ct)
        {
            var data = await _service.IssueIngredientsByProductionPlanAsync(franchiseId, productionPlanId, request, ct);
            return Ok(ApiResponse<IssueIngredientsByProductionPlanResponse>.Ok(data));
        }


        [HttpPost("adjustment")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<AdjustIngredientInventoryResponse>>> Adjust(
            int franchiseId,
            [FromBody] AdjustIngredientInventoryDto request,
            CancellationToken ct)
        {
            var data = await _service.AdjustIngredientAsync(franchiseId, request, ct);
            return Ok(ApiResponse<AdjustIngredientInventoryResponse>.Ok(data));
        }

        [HttpPost("InboundProduct")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<ProductInboundResponse>>> Inbound(
        int franchiseId,
        [FromBody] CreateProductInboundDto request,
        CancellationToken ct)
        {
            var data = await _service.InboundProductAsync(franchiseId, request, ct);
            return Ok(ApiResponse<ProductInboundResponse>.Ok(data));
        }
    }
}
