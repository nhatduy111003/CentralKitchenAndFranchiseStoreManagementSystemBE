using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Allocations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CentralKitchenAndFranchise.API.Controllers
{
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Manager},{RoleNames.SupplyCoordinator},{RoleNames.KitchenStaff}")]
    [ApiController]
    [Route("api/supply/allocations")]
    public class AllocationController : ControllerBase
    {
        private readonly IAllocationService _service;

        public AllocationController(IAllocationService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateAllocationDto dto)
        {
            var id = await _service.CreateAsync(dto);
            return Ok(id);
        }

        [HttpPost("{id}/items")]
        public async Task<IActionResult> AddItem(int id, AddAllocationItemDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _service.AddItemAsync(id, dto);
            return Ok();
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
            => Ok(await _service.GetAsync(id));

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(await _service.GetAllAsync());

        [HttpPut("items/{itemId}")]
        public async Task<IActionResult> UpdateItem(int itemId, decimal quantity)
        {
            await _service.UpdateItemAsync(itemId, quantity);
            return Ok();
        }

        [HttpDelete("items/{itemId}")]
        public async Task<IActionResult> RemoveItem(int itemId)
        {
            await _service.RemoveItemAsync(itemId);
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return Ok();
        }
    }

}
