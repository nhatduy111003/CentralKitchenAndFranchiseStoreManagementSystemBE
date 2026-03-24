using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Responses.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class DashboardScopeService : IDashboardScopeService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public DashboardScopeService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    /// <summary>Resolve the effective central-kitchen scope for dashboard requests.</summary>
    public async Task<DashboardCentralKitchenScope> ResolveCentralKitchenScopeAsync(int? requestedCentralKitchenId, CancellationToken ct = default)
    {
        if (_current.IsInRole(RoleNames.KitchenStaff) || _current.IsInRole(RoleNames.SupplyCoordinator))
        {
            var assignedCentralKitchenId = await GetAssignedCentralKitchenIdAsync(ct);
            var scope = await LoadCentralKitchenScopeAsync(assignedCentralKitchenId, ct);

            if (requestedCentralKitchenId.HasValue && requestedCentralKitchenId.Value != assignedCentralKitchenId)
                throw new ForbiddenAccessException("You do not have access to this central kitchen.");

            return scope;
        }

        if (_current.IsInRole(RoleNames.Admin) || _current.IsInRole(RoleNames.Manager))
        {
            if (!requestedCentralKitchenId.HasValue || requestedCentralKitchenId.Value <= 0)
                throw new ArgumentException("centralKitchenId is required for Admin/Manager dashboard scope.");

            return await LoadCentralKitchenScopeAsync(requestedCentralKitchenId.Value, ct);
        }

        throw new ForbiddenAccessException("You do not have permission to access this dashboard.");
    }

    /// <summary>Resolve the effective franchise scope for dashboard requests.</summary>
    public async Task<DashboardFranchiseScope> ResolveFranchiseScopeAsync(int? requestedFranchiseId, CancellationToken ct = default)
    {
        if (_current.IsInRole(RoleNames.StoreStaff))
        {
            var assignedFranchiseId = await GetAssignedFranchiseIdAsync(ct);
            var scope = await LoadFranchiseScopeAsync(assignedFranchiseId, ct);

            if (requestedFranchiseId.HasValue && requestedFranchiseId.Value != assignedFranchiseId)
                throw new ForbiddenAccessException("You do not have access to this franchise.");

            return scope;
        }

        if (_current.IsInRole(RoleNames.Admin) || _current.IsInRole(RoleNames.Manager))
        {
            if (!requestedFranchiseId.HasValue || requestedFranchiseId.Value <= 0)
                throw new ArgumentException("franchiseId is required for Admin/Manager dashboard scope.");

            return await LoadFranchiseScopeAsync(requestedFranchiseId.Value, ct);
        }

        throw new ForbiddenAccessException("You do not have permission to access this dashboard.");
    }

    /// <summary>Return all active franchise ids for the selected central kitchen.</summary>
    public async Task<List<int>> GetActiveFranchiseIdsByCentralKitchenAsync(int centralKitchenId, CancellationToken ct = default)
    {
        return await _db.Franchises
            .AsNoTracking()
            .Where(x => x.CentralKitchenId == centralKitchenId && x.Status == OrganizationStatus.Active)
            .Select(x => x.FranchiseId)
            .ToListAsync(ct);
    }

    /// <summary>Return all active franchise ids in the system.</summary>
    public async Task<List<int>> GetAllActiveFranchiseIdsAsync(CancellationToken ct = default)
    {
        return await _db.Franchises
            .AsNoTracking()
            .Where(x => x.Status == OrganizationStatus.Active)
            .Select(x => x.FranchiseId)
            .ToListAsync(ct);
    }

    /// <summary>Load a validated active central-kitchen scope with display name.</summary>
    private async Task<DashboardCentralKitchenScope> LoadCentralKitchenScopeAsync(int centralKitchenId, CancellationToken ct)
    {
        var scope = await _db.CentralKitchens
            .AsNoTracking()
            .Where(x => x.CentralKitchenId == centralKitchenId && x.Status == OrganizationStatus.Active)
            .Select(x => new DashboardCentralKitchenScope
            {
                CentralKitchenId = x.CentralKitchenId,
                CentralKitchenName = x.Name
            })
            .FirstOrDefaultAsync(ct);

        if (scope is null)
            throw new KeyNotFoundException("Central kitchen not found.");

        return scope;
    }

    /// <summary>Load a validated active franchise scope with related central-kitchen info.</summary>
    private async Task<DashboardFranchiseScope> LoadFranchiseScopeAsync(int franchiseId, CancellationToken ct)
    {
        var scope = await _db.Franchises
            .AsNoTracking()
            .Where(x => x.FranchiseId == franchiseId && x.Status == OrganizationStatus.Active)
            .Select(x => new DashboardFranchiseScope
            {
                FranchiseId = x.FranchiseId,
                FranchiseName = x.Name,
                CentralKitchenId = x.CentralKitchenId,
                CentralKitchenName = x.CentralKitchen.Name
            })
            .FirstOrDefaultAsync(ct);

        if (scope is null)
            throw new KeyNotFoundException("Franchise not found.");

        return scope;
    }

    /// <summary>Resolve the latest assigned central-kitchen id for the current scoped user.</summary>
    private async Task<int> GetAssignedCentralKitchenIdAsync(CancellationToken ct)
    {
        var centralKitchenId = await _db.UserWorkAssignments
            .AsNoTracking()
            .Where(x =>
                x.UserId == _current.UserId &&
                x.AssignmentType == WorkAssignmentTypes.CentralKitchen &&
                x.CentralKitchenId.HasValue)
            .OrderByDescending(x => x.AssignedAt)
            .ThenByDescending(x => x.UserWorkAssignmentId)
            .Select(x => x.CentralKitchenId)
            .FirstOrDefaultAsync(ct);

        if (!centralKitchenId.HasValue)
            throw new ForbiddenAccessException("Current user is not assigned to any central kitchen.");

        return centralKitchenId.Value;
    }

    /// <summary>Resolve the latest assigned franchise id for the current store staff.</summary>
    private async Task<int> GetAssignedFranchiseIdAsync(CancellationToken ct)
    {
        var franchiseId = await _db.UserWorkAssignments
            .AsNoTracking()
            .Where(x =>
                x.UserId == _current.UserId &&
                x.AssignmentType == WorkAssignmentTypes.Franchise &&
                x.FranchiseId.HasValue)
            .OrderByDescending(x => x.AssignedAt)
            .ThenByDescending(x => x.UserWorkAssignmentId)
            .Select(x => x.FranchiseId)
            .FirstOrDefaultAsync(ct);

        if (!franchiseId.HasValue)
            throw new ForbiddenAccessException("Current user is not assigned to any franchise.");

        return franchiseId.Value;
    }
}