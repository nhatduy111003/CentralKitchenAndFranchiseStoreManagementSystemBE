using CentralKitchenAndFranchise.DAL.Repositories.Interfaces;

namespace CentralKitchenAndFranchise.DAL.UnitOfWork;

public interface IUnitOfWork
{
    IUserRepository Users { get; }
    IIngredientRepository Ingredients { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
