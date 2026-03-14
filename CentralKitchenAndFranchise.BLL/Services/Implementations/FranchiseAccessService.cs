// CentralKitchenAndFranchise.BLL/Services/Implementations/FranchiseAccessService.cs
using CentralKitchenAndFranchise.BLL.Exceptions;
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
        if (franchiseId <= 0)
            throw new ArgumentException("franchiseId must be a positive integer.");

        if (_current.IsInRole(RoleNames.Admin) || _current.IsInRole(RoleNames.Manager))
            return;

        if (_current.IsInRole(RoleNames.StoreStaff))
        {
            var ok = await _db.UserWorkAssignments
                .AsNoTracking()
                .AnyAsync(x =>
                    x.UserId == _current.UserId &&
                    x.AssignmentType == WorkAssignmentTypes.Franchise &&
                    x.FranchiseId == franchiseId, ct);

            if (!ok)
                throw new ForbiddenAccessException("You do not have access to this franchise.");

            return;
        }

        if (_current.IsInRole(RoleNames.SupplyCoordinator))
        {
            var assignedCentralKitchenId = await GetCurrentAssignedCentralKitchenIdAsync(ct);

            var ok = await _db.Franchises
                .AsNoTracking()
                .AnyAsync(x =>
                    x.FranchiseId == franchiseId &&
                    x.CentralKitchenId == assignedCentralKitchenId, ct);

            if (!ok)
                throw new ForbiddenAccessException("You do not have access to this franchise.");

            return;
        }

        throw new ForbiddenAccessException("You do not have permission to access this franchise.");
    }

    public async Task EnsureCanAccessCentralKitchenAsync(int ckId, CancellationToken ct = default)
    {
        if (ckId <= 0)
            throw new ArgumentException("centralKitchenId must be a positive integer.");

        if (_current.IsInRole(RoleNames.Admin) || _current.IsInRole(RoleNames.Manager))
            return;

        if (_current.IsInRole(RoleNames.KitchenStaff) || _current.IsInRole(RoleNames.SupplyCoordinator))
        {
            var assignedCentralKitchenId = await GetCurrentAssignedCentralKitchenIdAsync(ct);
            if (assignedCentralKitchenId != ckId)
                throw new ForbiddenAccessException("You do not have access to this central kitchen.");

            return;
        }

        throw new ForbiddenAccessException("You do not have permission to access this central kitchen.");
    }

    public async Task<int> GetCurrentAssignedCentralKitchenIdAsync(CancellationToken ct = default)
    {
        if (!_current.IsInRole(RoleNames.KitchenStaff) && !_current.IsInRole(RoleNames.SupplyCoordinator))
            throw new InvalidOperationException("Current role is not scoped by central kitchen assignment.");

        var assignedCentralKitchenId = await _db.UserWorkAssignments
            .AsNoTracking()
            .Where(x =>
                x.UserId == _current.UserId &&
                x.AssignmentType == WorkAssignmentTypes.CentralKitchen &&
                x.CentralKitchenId.HasValue)
            .OrderByDescending(x => x.AssignedAt)
            .ThenByDescending(x => x.UserWorkAssignmentId)
            .Select(x => x.CentralKitchenId)
            .FirstOrDefaultAsync(ct);

        if (!assignedCentralKitchenId.HasValue)
            throw new ForbiddenAccessException("Current user is not assigned to any central kitchen.");

        return assignedCentralKitchenId.Value;
    }
}
