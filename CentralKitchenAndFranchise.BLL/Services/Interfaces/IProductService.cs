using CentralKitchenAndFranchise.DTO.Requests.Products;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Products;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IProductService
{
    Task<PagedResult<ProductResponse>> SearchAsync(ProductListQuery query, CancellationToken ct = default);
    Task<ProductResponse> GetByIdAsync(int id, CancellationToken ct = default);
}
