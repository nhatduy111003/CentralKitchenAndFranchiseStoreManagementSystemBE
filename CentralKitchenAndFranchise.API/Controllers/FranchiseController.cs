using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers
{
    [ApiController]
    [Route("api/admin/franchises")]
    [Authorize(Roles = RoleNames.Admin)]
    public class FranchiseController : ControllerBase
    {
        private readonly IFranchiseService _service;

        public FranchiseController(IFranchiseService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(await _service.GetAllAsync());

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create(FranchiseCreateDto dto)
        {
            var id = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id }, null);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, FranchiseCreateDto dto)
        {
            var success = await _service.UpdateAsync(id, dto);
            return success ? NoContent() : NotFound();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _service.DeleteAsync(id);
            return success ? NoContent() : NotFound();
        }
    }
}
