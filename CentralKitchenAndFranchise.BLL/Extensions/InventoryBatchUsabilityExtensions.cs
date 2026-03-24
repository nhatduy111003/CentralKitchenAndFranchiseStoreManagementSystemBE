using CentralKitchenAndFranchise.DAL.Entities;

namespace CentralKitchenAndFranchise.BLL.Extensions;

public static class InventoryBatchUsabilityExtensions
{
    public static bool IsUsableNonExpired(this ProductBatch batch, DateOnly today)
    {
        if (batch.Quantity <= 0)
            return false;

        var expiredAt = batch.CalculateExpiredAt();
        return expiredAt is null || expiredAt.Value >= today;
    }

    public static bool IsUsableNonExpired(this IngredientBatch batch, DateOnly today)
    {
        if (batch.Quantity <= 0)
            return false;

        var expiredAt = batch.CalculateExpiredAt();
        return expiredAt is null || expiredAt.Value >= today;
    }
}