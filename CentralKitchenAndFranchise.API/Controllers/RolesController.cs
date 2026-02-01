using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers
{
    [ApiController]
    [Route("admin/roles")]
    [Authorize(Roles = RoleNames.Admin)]
    public class RolesController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RolesController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(await _roleService.GetAllAsync());

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var role = await _roleService.GetByIdAsync(id);
            if (role == null) return NotFound();
            return Ok(role);
        }

        [HttpPost]
        public async Task<IActionResult> Create(RoleRequestDto dto)
        {
            var role = await _roleService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = role.RoleId }, role);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, RoleRequestDto dto)
        {
            var success = await _roleService.UpdateAsync(id, dto);
            if (!success) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _roleService.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
