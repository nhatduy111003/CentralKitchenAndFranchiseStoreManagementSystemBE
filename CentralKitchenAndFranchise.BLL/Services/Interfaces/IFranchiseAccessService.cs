namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IFranchiseAccessService
{
    //Dùng cho Franchise
    Task EnsureCanAccessAsync(int franchiseId, CancellationToken ct = default);

    //Dùng cho Central kitchen
    Task EnsureCanAccessCentralKitchenAsync(int ckId, CancellationToken ct = default);

    // Dùng cho các role scoped theo CentralKitchen (KitchenStaff, SupplyCoordinator).
    Task<int> GetCurrentAssignedCentralKitchenIdAsync(CancellationToken ct = default);
}
