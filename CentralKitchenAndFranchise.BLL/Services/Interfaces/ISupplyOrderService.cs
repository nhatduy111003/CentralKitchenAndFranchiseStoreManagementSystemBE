using CentralKitchenAndFranchise.DTO.Requests.StoreOrders;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.StoreOrders;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface ISupplyOrderService
{
    Task<List<SupplyOrderQueueItemResponse>> GetQueueAsync(CancellationToken ct = default);

    // Return final-state supply orders that have left the active queue. - DELIVERED/CONFIRMED/CANCELLED
    Task<PagedResult<SupplyProcessedOrderResponse>> GetHistoryAsync(
        SupplyOrderListQuery query,
        CancellationToken ct = default);

    Task<OrderWorkflowActionResponse> PrepareDeliveryAsync(
        int orderId,
        PrepareDeliveryRequest request,
        CancellationToken ct = default);

    Task<OrderWorkflowActionResponse> UpdateDeliveryStatusAsync(
        int orderId,
        UpdateSupplyDeliveryStatusRequest request,
        CancellationToken ct = default);
}