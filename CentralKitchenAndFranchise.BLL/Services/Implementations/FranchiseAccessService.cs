using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DTO.Constants;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class FranchiseAccessService : IFranchiseAccessService
{
    private readonly ICurrentUserService _current;

    public FranchiseAccessService(ICurrentUserService current)
    {
        _current = current;
    }

    public Task EnsureCanAccessAsync(int franchiseId, CancellationToken ct = default)
    {
        // Admin: system-wide (support/debug ok)
        if (_current.IsInRole(RoleNames.Admin))
            return Task.CompletedTask;

        //  Manager is global for business
        if (_current.IsInRole(RoleNames.Manager))
            return Task.CompletedTask;

        // Other roles: default deny here (we can extend later when implementing StoreStaff/Coordinator/CK scope)
        throw new UnauthorizedAccessException("You do not have permission to access this franchise.");
    }
}
