using CentralKitchenAndFranchise.DTO.Requests.Ingredients;
using CentralKitchenAndFranchise.DTO.Responses.Ingredients;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IIngredientService
{
    Task<List<IngredientResponse>> GetAllAsync(CancellationToken ct = default);
    Task<IngredientResponse> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IngredientResponse> CreateAsync(CreateIngredientRequest request, CancellationToken ct = default);
    Task<IngredientResponse> UpdateAsync(int id, UpdateIngredientRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
