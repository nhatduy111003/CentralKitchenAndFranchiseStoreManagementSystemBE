using CentralKitchenAndFranchise.DTO.Requests.StoreOrders;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.StoreOrders;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IStoreOrderService
{
    Task<StoreOrderResponse> CreateAsync(int franchiseId, CreateStoreOrderRequest request, CancellationToken ct = default);
    Task<StoreOrderResponse> UpdateAsync(int franchiseId, int orderId, UpdateStoreOrderRequest request, CancellationToken ct = default);
    Task<StoreOrderResponse> SubmitAsync(int franchiseId, int orderId, CancellationToken ct = default);
    Task<StoreOrderResponse> CancelAsync(int franchiseId, int orderId, string? reason, CancellationToken ct = default);

    Task<PagedResult<StoreOrderResponse>> SearchAsync(int franchiseId, StoreOrderListQuery query, CancellationToken ct = default);
    Task<StoreOrderResponse> GetByIdAsync(int franchiseId, int orderId, CancellationToken ct = default);
}