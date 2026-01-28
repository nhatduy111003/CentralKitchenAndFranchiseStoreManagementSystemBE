namespace CentralKitchenAndFranchise.DTO.Requests.Auth;

public class LoginRequest
{
    public string UsernameOrEmail { get; set; } = default!;
    public string Password { get; set; } = default!;
}
