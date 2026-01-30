using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Repositories.Interfaces;
using CentralKitchenAndFranchise.DTO.Requests.Auth;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IRevokedTokenRepository _revokedTokenRepository;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var res = await _auth.LoginAsync(request, ct);
        return Ok(ApiResponse<LoginResponse>.Ok(res));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var jti = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrWhiteSpace(jti)) return BadRequest("Missing jti");

        // optional: lấy exp để lưu ExpiresAt
        DateTime? expiresAt = null;
        var exp = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Exp)?.Value;
        if (long.TryParse(exp, out var expUnix))
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;

        await _revokedTokenRepository.RevokeAsync(jti, expiresAt);
        return Ok(new { message = "Logged out" });
    }

}

