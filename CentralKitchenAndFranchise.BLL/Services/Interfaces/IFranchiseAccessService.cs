namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IFranchiseAccessService
{
    // Admin,Manager: luôn true. Role khác: false.
    Task EnsureCanAccessAsync(int franchiseId, CancellationToken ct = default);
    Task EnsureCanAccessCentralKitchenAsync(int ckId, CancellationToken ct = default);

}
