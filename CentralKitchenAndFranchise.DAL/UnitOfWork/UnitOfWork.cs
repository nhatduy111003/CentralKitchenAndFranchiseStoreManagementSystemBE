using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.Repositories.Interfaces;

namespace CentralKitchenAndFranchise.DAL.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public UnitOfWork(AppDbContext db, IUserRepository users, IIngredientRepository ingredients)
    {
        _db = db;
        Users = users;
        Ingredients = ingredients;
    }

    public IUserRepository Users { get; }
    public IIngredientRepository Ingredients { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
