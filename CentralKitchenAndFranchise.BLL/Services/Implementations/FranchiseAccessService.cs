// CentralKitchenAndFranchise.BLL/Services/Implementations/FranchiseAccessService.cs
using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;
using Microsoft.EntityFrameworkCore;
using CentralKitchenAndFranchise.DAL.Entities;

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
        if (franchiseId <= 0)
            throw new ArgumentException("franchiseId must be a positive integer.");

        if (_current.IsInRole(RoleNames.Admin) || _current.IsInRole(RoleNames.Manager))
            return;

        // Manager/StoreStaff scoped by user_franchises
        if ( _current.IsInRole(RoleNames.StoreStaff) || _current.IsInRole(RoleNames.KitchenStaff))
        {
            var ok = await _db.UserFranchises
                .AsNoTracking()
                .AnyAsync(x => x.UserId == _current.UserId && x.FranchiseId == franchiseId, ct);

            if (!ok)
                throw new ForbiddenAccessException("You do not have access to this franchise.");

            return;
        }

        throw new ForbiddenAccessException("You do not have permission to access this franchise.");
    }
}