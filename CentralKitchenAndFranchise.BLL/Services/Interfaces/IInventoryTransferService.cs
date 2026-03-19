using System.Threading;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IInventoryTransferService
{
    Task TransferDeliveryAsync(
        int deliveryId,
        int fromCentralKitchenId,
        int toFranchiseId,
        DateTime now,
        CancellationToken ct = default);
}