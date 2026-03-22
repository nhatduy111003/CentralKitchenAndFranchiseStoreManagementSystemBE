using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Inventory;
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
        //CRUD Ingredient/ProductBatch
        [HttpPost("ingredients/inbound")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<CentralKitchenIngredientBatchResponse>>> InboundIngredient(
            int centralKitchenId,
            [FromBody] CreateIngredientBatchDto request,
            CancellationToken ct)
        {
            var data = await _service.InboundCentralKitchenIngredientAsync(centralKitchenId, request, ct);
            return Ok(ApiResponse<CentralKitchenIngredientBatchResponse>.Ok(data));
        }

        [HttpPost("ingredients/adjustment")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<AdjustIngredientInventoryResponse>>> AdjustIngredient(
            int centralKitchenId,
            [FromBody] AdjustIngredientInventoryDto request,
            CancellationToken ct)
        {
            var data = await _service.AdjustCentralKitchenIngredientAsync(centralKitchenId, request, ct);
            return Ok(ApiResponse<AdjustIngredientInventoryResponse>.Ok(data));
        }

        [HttpGet("ingredients/batches")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff},{RoleNames.SupplyCoordinator}")]
        public async Task<ActionResult<ApiResponse<List<CentralKitchenIngredientBatchResponse>>>> GetIngredientBatches(
            int centralKitchenId,
            [FromQuery] int? ingredientId,
            [FromQuery] bool includeZero,
            CancellationToken ct)
        {
            var data = await _service.GetCentralKitchenIngredientBatchesAsync(centralKitchenId, ingredientId, includeZero, ct);
            return Ok(ApiResponse<List<CentralKitchenIngredientBatchResponse>>.Ok(data));
        }

        [HttpGet("ingredients/batches/{batchId:int}")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff},{RoleNames.SupplyCoordinator}")]
        public async Task<ActionResult<ApiResponse<CentralKitchenIngredientBatchResponse>>> GetIngredientBatchById(
            int centralKitchenId,
            int batchId,
            CancellationToken ct)
        {
            var data = await _service.GetCentralKitchenIngredientBatchByIdAsync(centralKitchenId, batchId, ct);
            return Ok(ApiResponse<CentralKitchenIngredientBatchResponse>.Ok(data));
        }

        [HttpPut("ingredients/batches/{batchId:int}/batch-code")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<CentralKitchenIngredientBatchResponse>>> UpdateIngredientBatchCode(
            int centralKitchenId,
            int batchId,
            [FromBody] UpdateBatchCodeRequest request,
            CancellationToken ct)
        {
            var data = await _service.UpdateCentralKitchenIngredientBatchCodeAsync(centralKitchenId, batchId, request, ct);
            return Ok(ApiResponse<CentralKitchenIngredientBatchResponse>.Ok(data));
        }

        [HttpDelete("ingredients/batches/{batchId:int}")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteIngredientBatch(
            int centralKitchenId,
            int batchId,
            CancellationToken ct)
        {
            await _service.DeleteCentralKitchenIngredientBatchAsync(centralKitchenId, batchId, ct);
            return Ok(ApiResponse<object>.Ok(new { message = "Ingredient batch deleted successfully." }));
        }

        [HttpPost("products/inbound")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<CentralKitchenProductBatchResponse>>> InboundProduct(
            int centralKitchenId,
            [FromBody] CreateCentralKitchenProductBatchDto request,
            CancellationToken ct)
        {
            var data = await _service.InboundCentralKitchenProductAsync(centralKitchenId, request, ct);
            return Ok(ApiResponse<CentralKitchenProductBatchResponse>.Ok(data));
        }

        [HttpPost("products/adjustment")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<AdjustProductInventoryResponse>>> AdjustProduct(
            int centralKitchenId,
            [FromBody] AdjustProductInventoryDto request,
            CancellationToken ct)
        {
            var data = await _service.AdjustCentralKitchenProductAsync(centralKitchenId, request, ct);
            return Ok(ApiResponse<AdjustProductInventoryResponse>.Ok(data));
        }

        [HttpGet("products/batches")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff},{RoleNames.SupplyCoordinator}")]
        public async Task<ActionResult<ApiResponse<List<CentralKitchenProductBatchResponse>>>> GetProductBatches(
            int centralKitchenId,
            [FromQuery] int? productId,
            [FromQuery] bool includeZero,
            CancellationToken ct)
        {
            var data = await _service.GetCentralKitchenProductBatchesAsync(centralKitchenId, productId, includeZero, ct);
            return Ok(ApiResponse<List<CentralKitchenProductBatchResponse>>.Ok(data));
        }

        [HttpGet("products/batches/{batchId:int}")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff},{RoleNames.SupplyCoordinator}")]
        public async Task<ActionResult<ApiResponse<CentralKitchenProductBatchResponse>>> GetProductBatchById(
            int centralKitchenId,
            int batchId,
            CancellationToken ct)
        {
            var data = await _service.GetCentralKitchenProductBatchByIdAsync(centralKitchenId, batchId, ct);
            return Ok(ApiResponse<CentralKitchenProductBatchResponse>.Ok(data));
        }

        [HttpPut("products/batches/{batchId:int}/batch-code")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<CentralKitchenProductBatchResponse>>> UpdateProductBatchCode(
            int centralKitchenId,
            int batchId,
            [FromBody] UpdateBatchCodeRequest request,
            CancellationToken ct)
        {
            var data = await _service.UpdateCentralKitchenProductBatchCodeAsync(centralKitchenId, batchId, request, ct);
            return Ok(ApiResponse<CentralKitchenProductBatchResponse>.Ok(data));
        }

        [HttpDelete("products/batches/{batchId:int}")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteProductBatch(
            int centralKitchenId,
            int batchId,
            CancellationToken ct)
        {
            await _service.DeleteCentralKitchenProductBatchAsync(centralKitchenId, batchId, ct);
            return Ok(ApiResponse<object>.Ok(new { message = "Product batch deleted successfully." }));
        }
    }
}