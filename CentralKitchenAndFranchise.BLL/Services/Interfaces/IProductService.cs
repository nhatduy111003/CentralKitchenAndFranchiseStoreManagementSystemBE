using CentralKitchenAndFranchise.DTO.Requests.Products;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Products;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IProductService
{
    // READ (operational)
    Task<PagedResult<ProductResponse>> SearchAsync(ProductListQuery query, CancellationToken ct = default);
    Task<ProductResponse> GetByIdAsync(int id, CancellationToken ct = default);

    // WRITE (setup/master)
    Task<int> CreateAsync(ProductCreateRequest req, CancellationToken ct = default);
    Task UpdateAsync(int id, ProductUpdateRequest req, CancellationToken ct = default);
    Task ChangeStatusAsync(int id, ProductStatusUpdateRequest req, CancellationToken ct = default);
    Task DeactivateAsync(int id, CancellationToken ct = default);
}