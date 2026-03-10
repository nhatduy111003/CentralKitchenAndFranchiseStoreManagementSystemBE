using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.ProductionPlans;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.ProductionPlans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Numerics;

namespace CentralKitchenAndFranchise.API.Controllers
{
    [ApiController]
    [Route("api/central-kitchens/{centralKitchenId:int}/production-plans")]
    public class ProductionPlansController : ControllerBase
    {
        private readonly IProductionPlanService _service;

        public ProductionPlansController(IProductionPlanService service)
        {
            _service = service;
        }

        [HttpPost]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<ProductionPlanResponse>>> Create(
        int centralKitchenId,
        [FromBody] CreateProductionPlanDto request,
        CancellationToken ct)
        {
            var data = await _service.CreateAsync(centralKitchenId, request, ct);
            return Ok(ApiResponse<ProductionPlanResponse>.Ok(data));
        }

        public async Task<ActionResult<ApiResponse<ProductionPlanResponse>>> UpdateStatus(
        int centralKitchenId,
        int productionPlanId,
        [FromBody] UpdateProductionPlanStatusDto request,
        CancellationToken ct)
        {
            var data = await _service.UpdateStatusAsync(centralKitchenId, productionPlanId, request, ct);
            return Ok(ApiResponse<ProductionPlanResponse>.Ok(data));
        }

        [HttpGet("{productionPlanId:int}")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<ProductionPlanResponse>>> GetById(
        int centralKitchenId,
        int productionPlanId,
        CancellationToken ct)
        {
            var data = await _service.GetByIdAsync(centralKitchenId, productionPlanId, ct);
            return Ok(ApiResponse<ProductionPlanResponse>.Ok(data));
        }
    }
}
