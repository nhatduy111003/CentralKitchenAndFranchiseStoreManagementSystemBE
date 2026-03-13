using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Ingredients;
using CentralKitchenAndFranchise.DTO.Responses;
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
    }
}