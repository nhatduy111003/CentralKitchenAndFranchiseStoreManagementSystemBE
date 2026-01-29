using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CentralKitchenAndFranchise.DAL.UnitOfWork;
using CentralKitchenAndFranchise.DTO.Config;
using CentralKitchenAndFranchise.DTO.Requests.Auth;
using CentralKitchenAndFranchise.DTO.Responses.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class AuthService : Interfaces.IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly JwtOptions _jwt;

    public AuthService(IUnitOfWork uow, IOptions<JwtOptions> jwtOptions)
    {
        _uow = uow;
        _jwt = jwtOptions.Value;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Username/email and password are required.");

        var user = await _uow.Users.FindByUsernameOrEmailAsync(request.UsernameOrEmail, ct);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid credentials.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        if (!string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("User is inactive.");

        var expires = DateTime.UtcNow.AddMinutes(_jwt.ExpiresInMinutes);
        var token = GenerateJwt(user.UserId, user.Username, user.Role.Name, expires);

        return new LoginResponse
        {
            AccessToken = token,
            ExpiresInSeconds = (int)TimeSpan.FromMinutes(_jwt.ExpiresInMinutes).TotalSeconds,
            UserId = user.UserId,
            Username = user.Username,
            Role = user.Role.Name
        };
    }

    private string GenerateJwt(int userId, string username, string role, DateTime expiresUtc)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expiresUtc,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
