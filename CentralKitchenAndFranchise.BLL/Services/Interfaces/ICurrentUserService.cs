namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface ICurrentUserService
{
    int UserId { get; }
    string Role { get; }
    bool IsInRole(string role);
}
