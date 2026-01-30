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

    public AuthController(IAuthService auth, IRevokedTokenRepository revokedTokenRepository)
    {
        _auth = auth;
        _revokedTokenRepository = revokedTokenRepository;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var res = await _auth.LoginAsync(request, ct);
        return Ok(ApiResponse<LoginResponse>.Ok(res));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object>>> Logout()
    {
        var jti = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrWhiteSpace(jti))
            return BadRequest(ApiResponse<object>.Fail("Missing jti"));

        // exp claim có thể không tồn tại như claim => để null vẫn OK
        DateTime? expiresAt = null;

        await _revokedTokenRepository.RevokeAsync(jti, expiresAt);
        return Ok(ApiResponse<object>.Ok(new { message = "Logged out" }));
    }
}
