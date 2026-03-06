namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IFranchiseAccessService
{
    // Admin: luôn true. Manager: phải thuộc user_franchises. Role khác: false.
    Task EnsureCanAccessAsync(int franchiseId, CancellationToken ct = default);
}
