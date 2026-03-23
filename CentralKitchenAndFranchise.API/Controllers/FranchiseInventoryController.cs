using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Ingredients;
using CentralKitchenAndFranchise.DTO.Requests.Inventory;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers
{
    [ApiController]
    [Route("api/franchises/{franchiseId:int}/inventory")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
    public class FranchiseInventoryController : ControllerBase
    {
        private readonly IInventoryService _service;

        public FranchiseInventoryController(IInventoryService service)
        {
            _service = service;
        }

        [HttpPost("ingredients/inbound")]
        public async Task<ActionResult<ApiResponse<IngredientInboundResponse>>> InboundIngredient(
            int franchiseId,
            [FromBody] CreateIngredientInboundDto request,
            CancellationToken ct)
        {
            var data = await _service.InboundIngredientAsync(franchiseId, request, ct);
            return Ok(ApiResponse<IngredientInboundResponse>.Ok(data));
        }

        [HttpPost("ingredients/adjustment")]
        public async Task<ActionResult<ApiResponse<AdjustIngredientInventoryResponse>>> AdjustIngredient(
            int franchiseId,
            [FromBody] AdjustIngredientInventoryDto request,
            CancellationToken ct)
        {
            var data = await _service.AdjustIngredientAsync(franchiseId, request, ct);
            return Ok(ApiResponse<AdjustIngredientInventoryResponse>.Ok(data));
        }

        [HttpGet("ingredients/batches")]
        public async Task<ActionResult<ApiResponse<List<FranchiseIngredientBatchResponse>>>> GetIngredientBatches(
            int franchiseId,
            [FromQuery] int? ingredientId,
            [FromQuery] bool includeZero,
            CancellationToken ct)
        {
            var data = await _service.GetFranchiseIngredientBatchesAsync(franchiseId, ingredientId, includeZero, ct);
            return Ok(ApiResponse<List<FranchiseIngredientBatchResponse>>.Ok(data));
        }

        [HttpGet("ingredients/batches/{batchId:int}")]
        public async Task<ActionResult<ApiResponse<FranchiseIngredientBatchResponse>>> GetIngredientBatchById(
            int franchiseId,
            int batchId,
            CancellationToken ct)
        {
            var data = await _service.GetFranchiseIngredientBatchByIdAsync(franchiseId, batchId, ct);
            return Ok(ApiResponse<FranchiseIngredientBatchResponse>.Ok(data));
        }

        [HttpPut("ingredients/batches/{batchId:int}/batch-code")]
        public async Task<ActionResult<ApiResponse<FranchiseIngredientBatchResponse>>> UpdateIngredientBatchCode(
            int franchiseId,
            int batchId,
            [FromBody] UpdateBatchCodeRequest request,
            CancellationToken ct)
        {
            var data = await _service.UpdateFranchiseIngredientBatchCodeAsync(franchiseId, batchId, request, ct);
            return Ok(ApiResponse<FranchiseIngredientBatchResponse>.Ok(data));
        }

        [HttpDelete("ingredients/batches/{batchId:int}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteIngredientBatch(
            int franchiseId,
            int batchId,
            CancellationToken ct)
        {
            await _service.DeleteFranchiseIngredientBatchAsync(franchiseId, batchId, ct);
            return Ok(ApiResponse<object>.Ok(new { message = "Franchise ingredient batch deleted successfully." }));
        }

        [HttpPost("products/inbound")]
        public async Task<ActionResult<ApiResponse<ProductInboundResponse>>> InboundProduct(
            int franchiseId,
            [FromBody] CreateProductInboundDto request,
            CancellationToken ct)
        {
            var data = await _service.InboundProductAsync(franchiseId, request, ct);
            return Ok(ApiResponse<ProductInboundResponse>.Ok(data));
        }

        [HttpPost("products/adjustment")]
        public async Task<ActionResult<ApiResponse<AdjustProductInventoryResponse>>> AdjustProduct(
            int franchiseId,
            [FromBody] AdjustProductInventoryDto request,
            CancellationToken ct)
        {
            var data = await _service.AdjustProductAsync(franchiseId, request, ct);
            return Ok(ApiResponse<AdjustProductInventoryResponse>.Ok(data));
        }

        [HttpGet("products/batches")]
        public async Task<ActionResult<ApiResponse<List<FranchiseProductBatchResponse>>>> GetProductBatches(
            int franchiseId,
            [FromQuery] int? productId,
            [FromQuery] bool includeZero,
            CancellationToken ct)
        {
            var data = await _service.GetFranchiseProductBatchesAsync(franchiseId, productId, includeZero, ct);
            return Ok(ApiResponse<List<FranchiseProductBatchResponse>>.Ok(data));
        }

        [HttpGet("products/batches/{batchId:int}")]
        public async Task<ActionResult<ApiResponse<FranchiseProductBatchResponse>>> GetProductBatchById(
            int franchiseId,
            int batchId,
            CancellationToken ct)
        {
            var data = await _service.GetFranchiseProductBatchByIdAsync(franchiseId, batchId, ct);
            return Ok(ApiResponse<FranchiseProductBatchResponse>.Ok(data));
        }

        [HttpPut("products/batches/{batchId:int}/batch-code")]
        public async Task<ActionResult<ApiResponse<FranchiseProductBatchResponse>>> UpdateProductBatchCode(
            int franchiseId,
            int batchId,
            [FromBody] UpdateBatchCodeRequest request,
            CancellationToken ct)
        {
            var data = await _service.UpdateFranchiseProductBatchCodeAsync(franchiseId, batchId, request, ct);
            return Ok(ApiResponse<FranchiseProductBatchResponse>.Ok(data));
        }

        [HttpDelete("products/batches/{batchId:int}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteProductBatch(
            int franchiseId,
            int batchId,
            CancellationToken ct)
        {
            await _service.DeleteFranchiseProductBatchAsync(franchiseId, batchId, ct);
            return Ok(ApiResponse<object>.Ok(new { message = "Franchise product batch deleted successfully." }));
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(int franchiseId, CancellationToken ct)
        {
            var result = await _service.GetFranchiseInventorySummaryAsync(franchiseId, ct);
            return Ok(result);
        }
    }
}