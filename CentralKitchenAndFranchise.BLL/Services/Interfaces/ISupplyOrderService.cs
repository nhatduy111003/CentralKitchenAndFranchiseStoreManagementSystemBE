using CentralKitchenAndFranchise.DTO.Requests.StoreOrders;
using CentralKitchenAndFranchise.DTO.Responses.StoreOrders;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface ISupplyOrderService
{
    Task<List<SupplyOrderQueueItemResponse>> GetQueueAsync(CancellationToken ct = default);

    Task<OrderWorkflowActionResponse> PrepareDeliveryAsync(
        int orderId,
        PrepareDeliveryRequest request,
        CancellationToken ct = default);

    Task<OrderWorkflowActionResponse> UpdateDeliveryStatusAsync(
        int orderId,
        UpdateSupplyDeliveryStatusRequest request,
        CancellationToken ct = default);
}