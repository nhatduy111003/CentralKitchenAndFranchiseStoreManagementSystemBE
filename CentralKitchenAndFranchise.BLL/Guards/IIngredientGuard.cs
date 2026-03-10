using CentralKitchenAndFranchise.DAL.Entities;

namespace CentralKitchenAndFranchise.BLL.Guards;

public interface IIngredientGuard
{
    Task<Ingredient> RequireActiveAsync(int ingredientId, CancellationToken ct = default);
}
