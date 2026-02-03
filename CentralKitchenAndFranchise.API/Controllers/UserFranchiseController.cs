using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Requests;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CentralKitchenAndFranchise.API.Controllers
{
    [ApiController]
    [Route("admin/user-franchises")]
    public class UserFranchiseController : ControllerBase
    {
        private readonly IUserFranchiseService _service;

        public UserFranchiseController(IUserFranchiseService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Assign(AssignUserFranchiseDto dto)
        {
            await _service.AssignAsync(dto.UserId, dto.FranchiseId);
            return Ok("Assigned");
        }

        [HttpDelete]
        public async Task<IActionResult> Remove(int userId, int franchiseId)
        {
            await _service.RemoveAsync(userId, franchiseId);
            return Ok("Removed");
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> Get(int userId)
        {
            var list = await _service.GetByUserAsync(userId);
            return Ok(list);
        }
    }

}
