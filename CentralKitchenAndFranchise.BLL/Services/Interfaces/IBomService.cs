using CentralKitchenAndFranchise.DTO.Requests.Boms;
using CentralKitchenAndFranchise.DTO.Responses.Boms;
using CentralKitchenAndFranchise.DTO.Responses.Common;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IBomService
{
    Task<PagedResult<BomResponse>> SearchAsync(BomListQuery query, CancellationToken ct = default);
    Task<BomResponse> GetByIdAsync(int id, CancellationToken ct = default);
    Task<BomResponse> CreateAsync(CreateBomRequest request, CancellationToken ct = default);
    Task<BomResponse> UpdateAsync(int id, UpdateBomRequest request, CancellationToken ct = default);
    Task<BomResponse> ChangeStatusAsync(int id, ChangeBomStatusRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default); // soft delete => INACTIVE
}