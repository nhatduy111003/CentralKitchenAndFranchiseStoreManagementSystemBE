using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers
{
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager}")]
    [ApiController]
    [Route("api/admin/user-work-assignments")]
    public class UserWorkAssignmentController : ControllerBase
    {
        private readonly IUserWorkAssignmentService _service;

        public UserWorkAssignmentController(IUserWorkAssignmentService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Assign([FromBody] AssignUserWorkAssignmentDto dto)
        {
            await _service.AssignAsync(dto);
            return Ok("Assigned");
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] string assignmentType,
            [FromQuery] int? franchiseId,
            [FromQuery] int? centralKitchenId)
        {
            var users = await _service.GetUsersByAssignmentAsync(
                assignmentType,
                franchiseId,
                centralKitchenId);

            return Ok(users);
        }

        [HttpDelete("{userId:int}")]
        public async Task<IActionResult> Remove(int userId)
        {
            await _service.RemoveAsync(userId);
            return Ok("Removed");
        }

        [HttpGet("{userId:int}")]
        public async Task<IActionResult> GetByUser(int userId)
        {
            var assignment = await _service.GetByUserAsync(userId);
            return Ok(assignment);
        }
    }
}