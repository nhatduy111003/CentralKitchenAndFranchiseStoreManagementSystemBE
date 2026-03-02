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
    [Route("api/franchises/{franchiseId:int}/production-plans")]
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
            int franchiseId,
            [FromBody] CreateProductionPlanDto request,
            CancellationToken ct)
        {
            var data = await _service.CreateAsync(franchiseId, request, ct);
            return Ok(ApiResponse<ProductionPlanResponse>.Ok(data));
        }

        [HttpPatch("{productionPlanId:int}/status")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<ProductionPlanResponse>>> UpdateStatus(
            int franchiseId,
            int productionPlanId,
            [FromBody] UpdateProductionPlanStatusDto request,
            CancellationToken ct)
        {
            var data = await _service.UpdateStatusAsync(franchiseId, productionPlanId, request, ct);
            return Ok(ApiResponse<ProductionPlanResponse>.Ok(data));
        }

        [HttpGet("{productionPlanId:int}")]
        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.KitchenStaff}")]
        public async Task<ActionResult<ApiResponse<ProductionPlanResponse>>> GetById(
            int franchiseId,
            int productionPlanId,
            CancellationToken ct)
        {
            var data = await _service.GetByIdAsync(franchiseId, productionPlanId, ct);
            return Ok(ApiResponse<ProductionPlanResponse>.Ok(data));
        }
    }
}
