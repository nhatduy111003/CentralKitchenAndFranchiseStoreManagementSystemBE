using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.DAL.Repositories.Implementations;

public class IngredientRepository : IIngredientRepository
{
    private readonly AppDbContext _db;
    public IngredientRepository(AppDbContext db) => _db = db;

    public Task<Ingredient?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.Ingredients.FirstOrDefaultAsync(x => x.IngredientId == id, ct);

    public Task<List<Ingredient>> GetAllAsync(CancellationToken ct = default)
        => _db.Ingredients.OrderBy(x => x.IngredientId).ToListAsync(ct);

    public Task AddAsync(Ingredient entity, CancellationToken ct = default)
        => _db.Ingredients.AddAsync(entity, ct).AsTask();

    public void Update(Ingredient entity) => _db.Ingredients.Update(entity);

    public void Remove(Ingredient entity) => _db.Ingredients.Remove(entity);
}
