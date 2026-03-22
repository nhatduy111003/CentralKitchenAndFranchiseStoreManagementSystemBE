using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers
{
    [ApiController]
    [Route("api/central-kitchens/{centralKitchenId:int}/inventory")]
    public class CentralKitchenInventoryController : ControllerBase
    {
        private readonly IInventoryService _service;

        public CentralKitchenInventoryController(IInventoryService service)
        {
            _service = service;
        }

        [HttpGet("summary")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff},{RoleNames.SupplyCoordinator}")]
        public async Task<ActionResult<CentralKitchenInventorySummaryResponse>> GetSummary(
            int centralKitchenId,
            CancellationToken ct)
        {
            var data = await _service.GetCentralKitchenInventorySummaryAsync(centralKitchenId, ct);
            return Ok(data);
        }

        // Central Kitchen issue nguyên liệu theo production plan
        [HttpPost("ingredients/issue-by-production-plan/{productionPlanId:int}")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<IssueIngredientsByProductionPlanResponse>>> IssueByProductionPlan(
            int centralKitchenId,
            int productionPlanId,
            [FromBody] IssueIngredientsByProductionPlanDto request,
            CancellationToken ct)
        {
            var data = await _service.IssueIngredientsByProductionPlanAsync(
                centralKitchenId,
                productionPlanId,
                request,
                ct);

            return Ok(ApiResponse<IssueIngredientsByProductionPlanResponse>.Ok(data));
        }
    }
}