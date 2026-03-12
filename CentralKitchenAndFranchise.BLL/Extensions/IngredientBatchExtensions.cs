using CentralKitchenAndFranchise.DAL.Entities;

namespace CentralKitchenAndFranchise.BLL.Extensions;

public static class IngredientBatchExtensions
{
    public static DateOnly? CalculateExpiredAt(this IngredientBatch batch)
    {
        if (batch.Ingredient is null)
            throw new InvalidOperationException("Ingredient must be loaded to calculate ExpiredAt.");

        if (batch.Ingredient.ShelfLifeDays <= 0)
            return null;

        return DateOnly.FromDateTime(
            batch.CreatedAt.Date.AddDays(batch.Ingredient.ShelfLifeDays)
        );
    }
}