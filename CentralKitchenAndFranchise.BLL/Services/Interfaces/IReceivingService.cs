using CentralKitchenAndFranchise.DTO.Requests.Receivings;
using CentralKitchenAndFranchise.DTO.Responses.Inventory;
using CentralKitchenAndFranchise.DTO.Responses.Receivings;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IReceivingService
{
    Task<List<ReceivingListItemResponse>> GetPendingAsync(
        int franchiseId,
        CancellationToken ct = default);

    Task<ReceivingDetailResponse> GetByIdAsync(
        int franchiseId,
        int deliveryId,
        CancellationToken ct = default);

    Task<ReceivingConfirmResponse> ConfirmAsync(
        int franchiseId,
        int deliveryId,
        ConfirmReceivingRequest request,
        CancellationToken ct = default);

}