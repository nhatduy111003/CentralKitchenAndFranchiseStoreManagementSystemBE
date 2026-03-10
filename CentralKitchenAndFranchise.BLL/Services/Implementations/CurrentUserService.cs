using System.Security.Claims;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor http) => _http = http;

    public int UserId
    {
        get
        {
            var raw = _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(raw) || !int.TryParse(raw, out var id))
                throw new UnauthorizedAccessException("Missing user id in token.");
            return id;
        }
    }

    public string Role
    {
        get
        {
            var role = _http.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);
            if (string.IsNullOrWhiteSpace(role))
                throw new UnauthorizedAccessException("Missing role in token.");
            return role;
        }
    }

    public bool IsInRole(string role)
        => string.Equals(Role, role, StringComparison.OrdinalIgnoreCase);
}
