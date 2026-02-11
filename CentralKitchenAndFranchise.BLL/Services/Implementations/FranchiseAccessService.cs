// CentralKitchenAndFranchise.BLL/Services/Implementations/FranchiseAccessService.cs
using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

/// <summary>
/// Franchise scope enforcement.
///
/// Rules:
/// - Admin: system-wide access.
/// - Manager: access only to franchises assigned via user_franchises.
/// - Others: deny for now (extend later when implementing StoreStaff/Coordinator/CK scope).
///
/// NOTE: List franchises endpoint is an explicit exception (Manager can see all) and MUST NOT call this service.
/// </summary>
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

        // Admin: system-wide
        if (_current.IsInRole(RoleNames.Admin))
            return;

        // Manager: scoped by user_franchises
        if (_current.IsInRole(RoleNames.Manager))
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
