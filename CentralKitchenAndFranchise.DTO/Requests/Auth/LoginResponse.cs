namespace CentralKitchenAndFranchise.DTO.Responses.Auth;

public class LoginResponse
{
    public string AccessToken { get; set; } = default!;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresInSeconds { get; set; }

    public int UserId { get; set; }
    public int FranchiseId { get; set; }
    public int CentralKitchenId { get; set; }
    public string Username { get; set; } = default!;
    public string Role { get; set; } = default!;
}
