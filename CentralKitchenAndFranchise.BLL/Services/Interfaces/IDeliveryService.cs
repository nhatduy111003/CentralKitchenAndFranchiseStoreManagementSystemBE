using CentralKitchenAndFranchise.DTO.Requests.Deliveries;
using CentralKitchenAndFranchise.DTO.Responses.Deliveries;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IDeliveryService
{
    Task<int> CreatePlanAsync(CreateDeliveryPlanRequest request, CancellationToken ct = default);
    Task<int> CreateDeliveryAsync(CreateDeliveryRequest request, CancellationToken ct = default);

    Task<DeliveryDetailsResponse> GetByIdAsync(int deliveryId, CancellationToken ct = default);

    Task UpsertProductItemsAsync(int deliveryId, List<UpsertDeliveryProductItemRequest> items, CancellationToken ct = default);
    Task UpsertIngredientItemsAsync(int deliveryId, List<UpsertDeliveryIngredientItemRequest> items, CancellationToken ct = default);

    Task ConfirmAsync(int deliveryId, CancellationToken ct = default);
}
