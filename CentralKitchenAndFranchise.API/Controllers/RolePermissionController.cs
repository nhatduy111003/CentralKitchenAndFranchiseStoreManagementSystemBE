using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Requests;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CentralKitchenAndFranchise.API.Controllers
{
    [ApiController]
    [Route("api/admin/role-permissions")]
    public class RolePermissionController : ControllerBase
    {
        private readonly IRolePermissionService _service;

        public RolePermissionController(IRolePermissionService service)
        {
            _service = service;
        }

        // assign permission vào role
        [HttpPost]
        public async Task<IActionResult> Add(AddRolePermissionDto dto)
        {
            await _service.AssignToRoleAsync(dto.RoleId, dto.PermissionId);
            return Ok("Assigned successfully");
        }

        // remove permission khỏi role
        [HttpDelete]
        public async Task<IActionResult> Remove([FromQuery] int roleId, [FromQuery] int permissionId)
        {
            await _service.RemovePermissionAsync(roleId, permissionId);
            return Ok("Removed");
        }

        // lấy permission theo role
        [HttpGet("{roleId}")]
        public async Task<IActionResult> Get(int roleId)
        {
            var list = await _service.GetPermissionsByRoleAsync(roleId);
            return Ok(list);
        }
    }

}
