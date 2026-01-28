using CentralKitchenAndFranchise.DAL.Entities;

namespace CentralKitchenAndFranchise.DAL.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> FindByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken ct = default);
}
