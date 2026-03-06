using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.DAL.Repositories.Implementations;

public class SupplierRepository : ISupplierRepository
{
    private readonly AppDbContext _db;
    public SupplierRepository(AppDbContext db) => _db = db;

    public Task<Supplier?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == id, ct);

    public Task<List<Supplier>> GetAllAsync(CancellationToken ct = default)
        => _db.Suppliers.OrderBy(x => x.SupplierId).ToListAsync(ct);

    public Task AddAsync(Supplier entity, CancellationToken ct = default)
        => _db.Suppliers.AddAsync(entity, ct).AsTask();

    public void Update(Supplier entity) => _db.Suppliers.Update(entity);

    public void Remove(Supplier entity) => _db.Suppliers.Remove(entity);
}
