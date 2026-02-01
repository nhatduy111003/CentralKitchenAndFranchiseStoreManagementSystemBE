using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.Repositories.Interfaces;

namespace CentralKitchenAndFranchise.DAL.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public UnitOfWork(
        AppDbContext db,
        IUserRepository users,
        IIngredientRepository ingredients,
        ISupplierRepository suppliers)
    {
        _db = db;
        Users = users;
        Ingredients = ingredients;
        Suppliers = suppliers;
    }

    public IUserRepository Users { get; }
    public IIngredientRepository Ingredients { get; }
    public ISupplierRepository Suppliers { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
