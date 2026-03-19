using CentralKitchenAndFranchise.DTO.Requests.StoreOrders;
using CentralKitchenAndFranchise.DTO.Responses.StoreOrders;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IKitchenOrderService
{
    Task<IncomingOrderDetailResponse> GetDetailAsync(int centralKitchenId, int orderId, CancellationToken ct = default);

    Task<OrderWorkflowActionResponse> ReceiveAsync(
        int centralKitchenId,
        int orderId,
        ReceiveIncomingOrderRequest request,
        CancellationToken ct = default);

    Task<OrderWorkflowActionResponse> UpdateProcessingNoteAsync(
        int centralKitchenId,
        int orderId,
        UpdateProcessingNoteRequest request,
        CancellationToken ct = default);

    Task<OrderWorkflowActionResponse> ForwardToSupplyAsync(
        int centralKitchenId,
        int orderId,
        ForwardToSupplyRequest request,
        CancellationToken ct = default);

    Task<List<StoreOrderHistoryResponse>> GetHistoryAsync(int centralKitchenId, int orderId, CancellationToken ct = default);
}