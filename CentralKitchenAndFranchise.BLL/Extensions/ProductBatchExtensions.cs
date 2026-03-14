using CentralKitchenAndFranchise.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Extensions
{
    public static class ProductBatchExtensions
    {
        public static DateOnly? CalculateExpiredAt(this ProductBatch batch)
        {
            if (batch.Product is null)
                throw new InvalidOperationException("Product must be loaded to calculate ExpiredAt.");

            if (batch.Product.ShelfLifeDays <= 0)
                return null;

            return DateOnly.FromDateTime(
                batch.CreatedAt.Date.AddDays(batch.Product.ShelfLifeDays)
            );
        }
    }
}
