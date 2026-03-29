using System.Threading;
using System.Threading.Tasks;
using CentralKitchenAndFranchise.DAL.Entities;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IInventoryTransferService
{
    Task CommitDeliveryStockAsync(
        Delivery delivery,
        int toFranchiseId,
        DateTime now,
        CancellationToken ct = default);

    Task TransferDeliveryAsync(
        int deliveryId,
        int fromCentralKitchenId,
        int toFranchiseId,
        DateTime now,
        CancellationToken ct = default);
}