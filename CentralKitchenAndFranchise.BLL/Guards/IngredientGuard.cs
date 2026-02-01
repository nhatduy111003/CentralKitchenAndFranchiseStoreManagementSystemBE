using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Guards;

public class IngredientGuard : IIngredientGuard
{
    private readonly AppDbContext _db;

    public IngredientGuard(AppDbContext db) => _db = db;

    public async Task<Ingredient> RequireActiveAsync(int ingredientId, CancellationToken ct = default)
    {
        var ing = await _db.Ingredients
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IngredientId == ingredientId, ct);

        if (ing is null)
            throw new KeyNotFoundException($"Ingredient {ingredientId} not found.");

        if (!string.Equals(ing.Status, IngredientStatus.Active, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Ingredient {ingredientId} is INACTIVE and cannot be used.");

        return ing;
    }
}
