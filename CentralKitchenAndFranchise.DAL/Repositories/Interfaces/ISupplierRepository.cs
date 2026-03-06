using CentralKitchenAndFranchise.DAL.Entities;

namespace CentralKitchenAndFranchise.DAL.Repositories.Interfaces;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Supplier>> GetAllAsync(CancellationToken ct = default);

    Task AddAsync(Supplier entity, CancellationToken ct = default);
    void Update(Supplier entity);
    void Remove(Supplier entity);
}
