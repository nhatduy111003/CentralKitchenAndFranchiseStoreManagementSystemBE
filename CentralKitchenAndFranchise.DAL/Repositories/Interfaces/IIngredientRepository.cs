using CentralKitchenAndFranchise.DAL.Entities;

namespace CentralKitchenAndFranchise.DAL.Repositories.Interfaces;

public interface IIngredientRepository
{
    Task<Ingredient?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Ingredient>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Ingredient entity, CancellationToken ct = default);
    void Update(Ingredient entity);
    void Remove(Ingredient entity);
}
