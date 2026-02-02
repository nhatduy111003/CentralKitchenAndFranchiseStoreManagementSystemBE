using CentralKitchenAndFranchise.DTO.Requests.StoreCatalog;
using CentralKitchenAndFranchise.DTO.Requests.StoreCatalogs;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.StoreCatalog;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IStoreCatalogService
{
    Task<PagedResult<StoreCatalogResponse>> SearchAsync(StoreCatalogListQuery query, CancellationToken ct = default);
    Task<StoreCatalogResponse> GetByKeyAsync(int franchiseId, int productId, CancellationToken ct = default);

    Task<StoreCatalogResponse> AssignAsync(UpsertStoreCatalogRequest request, CancellationToken ct = default);
    Task<StoreCatalogResponse> UpdateAsync(int franchiseId, int productId, UpdateStoreCatalogRequest request, CancellationToken ct = default);
    Task<StoreCatalogResponse> ChangeStatusAsync(int franchiseId, int productId, ChangeStoreCatalogStatusRequest request, CancellationToken ct = default);
    Task DeleteAsync(int franchiseId, int productId, CancellationToken ct = default);
}
