using CentralKitchenAndFranchise.DTO.Requests.Recipes;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Recipes;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IRecipeService
{
    Task<PagedResult<RecipeResponse>> SearchAsync(RecipeListQuery query, CancellationToken ct = default);
    Task<RecipeResponse> GetByIdAsync(int id, CancellationToken ct = default);
    Task<RecipeResponse> CreateAsync(CreateRecipeRequest request, CancellationToken ct = default);
    Task<RecipeResponse> UpdateAsync(int id, UpdateRecipeRequest request, CancellationToken ct = default);
    Task<RecipeResponse> ChangeStatusAsync(int id, ChangeRecipeStatusRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default); // soft delete => INACTIVE
}