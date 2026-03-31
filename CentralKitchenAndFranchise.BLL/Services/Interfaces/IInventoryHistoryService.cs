using CentralKitchenAndFranchise.DTO.Requests.InventoryHistory;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.InventoryHistory;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IInventoryHistoryService
{
    Task<PagedResult<InventoryHistoryMovementResponse>> GetFranchiseMovementsAsync(
        int franchiseId,
        InventoryHistoryMovementsQuery query,
        CancellationToken ct = default);

    Task<PagedResult<InventoryHistoryMovementResponse>> GetCentralKitchenMovementsAsync(
        int centralKitchenId,
        InventoryHistoryMovementsQuery query,
        CancellationToken ct = default);

    Task<InventoryBatchLifecycleResponse> GetFranchiseBatchLifecycleAsync(
        int franchiseId,
        int batchId,
        InventoryBatchLifecycleQuery? query,
        CancellationToken ct = default);

    Task<InventoryBatchLifecycleResponse> GetCentralKitchenBatchLifecycleAsync(
        int centralKitchenId,
        int batchId,
        InventoryBatchLifecycleQuery? query,
        CancellationToken ct = default);
}
