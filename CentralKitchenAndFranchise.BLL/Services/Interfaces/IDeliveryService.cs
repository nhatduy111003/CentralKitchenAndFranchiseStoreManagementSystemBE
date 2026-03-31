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

    // Update one existing product line quantity before prepare commits stock.
    Task UpdateProductItemQuantityAsync(int deliveryId, int productId, decimal quantity, CancellationToken ct = default);

    // Update one existing ingredient line quantity before prepare commits stock.
    Task UpdateIngredientItemQuantityAsync(int deliveryId, int ingredientId, decimal quantity, CancellationToken ct = default);

    Task<DeliveryDetailsResponse> ConfirmAsync(int deliveryId, CancellationToken ct = default);
}