using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Ingredients;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers
{
    [ApiController]
    [Route("api/franchises/{franchiseId:int}/inventory")]
    public class FranchiseInventoryController : ControllerBase
    {
        private readonly IInventoryService _service;

        public FranchiseInventoryController(IInventoryService service)
        {
            _service = service;
        }

        // Franchise nhập kho nguyên liệu
        [HttpPost("ingredients/inbound")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
        public async Task<ActionResult<ApiResponse<IngredientInboundResponse>>> InboundIngredient(
            int franchiseId,
            [FromBody] CreateIngredientInboundDto request,
            CancellationToken ct)
        {
            var data = await _service.InboundIngredientAsync(franchiseId, request, ct);
            return Ok(ApiResponse<IngredientInboundResponse>.Ok(data));
        }

        // Franchise điều chỉnh tồn kho nguyên liệu
        [HttpPost("ingredients/adjustment")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
        public async Task<ActionResult<ApiResponse<AdjustIngredientInventoryResponse>>> AdjustIngredient(
            int franchiseId,
            [FromBody] AdjustIngredientInventoryDto request,
            CancellationToken ct)
        {
            var data = await _service.AdjustIngredientAsync(franchiseId, request, ct);
            return Ok(ApiResponse<AdjustIngredientInventoryResponse>.Ok(data));
        }

        // Franchise nhập kho thành phẩm
        [HttpPost("products/inbound")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
        public async Task<ActionResult<ApiResponse<ProductInboundResponse>>> InboundProduct(
            int franchiseId,
            [FromBody] CreateProductInboundDto request,
            CancellationToken ct)
        {
            var data = await _service.InboundProductAsync(franchiseId, request, ct);
            return Ok(ApiResponse<ProductInboundResponse>.Ok(data));
        }

        [HttpGet("ingredients")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<PagedResult<StoreIngredientInventoryResponse>>>> GetIngredientInventory(
            int franchiseId,
            [FromQuery] InventoryListQuery query,
            CancellationToken ct)
        {
            var data = await _service.GetStoreIngredientInventoryAsync(franchiseId, query, ct);
            return Ok(ApiResponse<PagedResult<StoreIngredientInventoryResponse>>.Ok(data));
        }

        [HttpGet("products")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<PagedResult<StoreProductInventoryResponse>>>> GetProductInventory(
            int franchiseId,
            [FromQuery] InventoryListQuery query,
            CancellationToken ct)
        {
            var data = await _service.GetStoreProductInventoryAsync(franchiseId, query, ct);
            return Ok(ApiResponse<PagedResult<StoreProductInventoryResponse>>.Ok(data));
        }

        [HttpGet("ingredients/history")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<PagedResult<IngredientInventoryHistoryResponse>>>> GetIngredientHistory(
            int franchiseId,
            [FromQuery] InventoryHistoryQuery query,
            CancellationToken ct)
        {
            var data = await _service.GetStoreIngredientHistoryAsync(franchiseId, query, ct);
            return Ok(ApiResponse<PagedResult<IngredientInventoryHistoryResponse>>.Ok(data));
        }

        [HttpGet("products/history")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<PagedResult<ProductInventoryHistoryResponse>>>> GetProductHistory(
            int franchiseId,
            [FromQuery] InventoryHistoryQuery query,
            CancellationToken ct)
        {
            var data = await _service.GetStoreProductHistoryAsync(franchiseId, query, ct);
            return Ok(ApiResponse<PagedResult<ProductInventoryHistoryResponse>>.Ok(data));
        }

        [HttpPost("ingredients/waste")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.StoreStaff}")]
        public async Task<ActionResult<ApiResponse<IngredientWasteResponse>>> CreateIngredientWaste(
            int franchiseId,
            [FromBody] CreateIngredientWasteDto request,
            CancellationToken ct)
        {
            var data = await _service.CreateIngredientWasteAsync(franchiseId, request, ct);
            return Ok(ApiResponse<IngredientWasteResponse>.Ok(data));
        }
    }
}