using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CentralKitchenAndFranchise.API.Controllers
{
    [ApiController]
    [Route("admin/roles")]
    public class AdminRolesController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetRoles()
        {
            return Ok(new[]
            {
            "Admin",
            "StoreStaff",
            "CentralKitchenStaff",
            "SupplyCoordinator"
        });
        }
    }
}
