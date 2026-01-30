using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants; // RoleNames
using CentralKitchenAndFranchise.DTO.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("admin/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    // =========================
    // Admin-only
    // =========================

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _userService.GetAllAsync());

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequestDto dto)
        => Ok(await _userService.CreateAsync(dto));

    [Authorize(Roles = RoleNames.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _userService.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }

    // =========================
    // Logged-in (any user) — nhưng chỉ được thao tác “chính mình”
    // =========================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!IsAdmin() && id != GetCurrentUserId())
            return Forbid();

        var user = await _userService.GetByIdAsync(id);
        return user == null ? NotFound() : Ok(user);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequestDto dto)
    {
        if (!IsAdmin() && id != GetCurrentUserId())
            return Forbid();

        var ok = await _userService.UpdateAsync(id, dto);
        return ok ? NoContent() : NotFound();
    }

    // =========================
    // helpers
    // =========================

    private bool IsAdmin() => User.IsInRole(RoleNames.Admin);

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        return int.TryParse(idStr, out var id) ? id : 0;
    }
}
