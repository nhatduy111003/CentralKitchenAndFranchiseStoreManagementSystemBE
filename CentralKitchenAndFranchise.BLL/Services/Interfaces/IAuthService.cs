using CentralKitchenAndFranchise.DTO.Requests.Auth;
using CentralKitchenAndFranchise.DTO.Responses.Auth;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}
