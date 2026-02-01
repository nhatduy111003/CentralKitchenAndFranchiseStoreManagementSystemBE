using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class FranchiseAccessService : IFranchiseAccessService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public FranchiseAccessService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task EnsureCanAccessAsync(int franchiseId, CancellationToken ct = default)
    {
        if (_current.IsInRole(RoleNames.Admin))
            return;

        if (_current.IsInRole(RoleNames.Manager))
        {
            var ok = await _db.UserFranchises.AnyAsync(x =>
                x.UserId == _current.UserId && x.FranchiseId == franchiseId, ct);

            if (ok) return;
            throw new UnauthorizedAccessException("Manager does not have access to this franchise.");
        }

        throw new UnauthorizedAccessException("You do not have permission to access this franchise.");
    }
}
