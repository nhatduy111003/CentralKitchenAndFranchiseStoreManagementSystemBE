using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.Persistence;
using CentralKitchenAndFranchise.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.DAL.Repositories.Implementations;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public Task<User?> FindByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken ct = default)
    {
        var key = usernameOrEmail.Trim().ToLower();
        return _db.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x =>
                x.Username.ToLower() == key || x.Email.ToLower() == key, ct);
    }
}
