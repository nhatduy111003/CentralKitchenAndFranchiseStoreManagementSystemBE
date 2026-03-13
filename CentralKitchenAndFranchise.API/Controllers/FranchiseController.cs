using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers
{
    [ApiController]
    [Route("api/admin/franchises")]
    public class FranchiseController : ControllerBase
    {
        private readonly IFranchiseService _service;

        public FranchiseController(IFranchiseService service)
        {
            _service = service;
        }

        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<FranchiseDto>>>> GetAll()
            => Ok(ApiResponse<List<FranchiseDto>>.Ok(await _service.GetAllAsync()));

        [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<FranchiseDto>>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return result is null
                ? NotFound(ApiResponse.Fail($"Franchise {id} not found.", errorCode: "NOT_FOUND"))
                : Ok(ApiResponse<FranchiseDto>.Ok(result));
        }
        [Authorize(Roles = RoleNames.Admin)]
        [HttpPost]
        public async Task<IActionResult> Create(FranchiseCreateDto dto)
        {
            var id = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id }, null);
        }

        [Authorize(Roles = RoleNames.Admin)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, FranchiseCreateDto dto)
        {
            var success = await _service.UpdateAsync(id, dto);
            return success ? NoContent() : NotFound(ApiResponse.Fail($"Franchise {id} not found.", errorCode: "NOT_FOUND"));
        }

        // ❗ Admin-only (hiện đang hard delete — sẽ chỉnh soft delete ở Task 18 nếu bạn muốn)
        [Authorize(Roles = RoleNames.Admin)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _service.DeleteAsync(id);
            return success ? NoContent() : NotFound(ApiResponse.Fail($"Franchise {id} not found.", errorCode: "NOT_FOUND"));
        }
    }
}
