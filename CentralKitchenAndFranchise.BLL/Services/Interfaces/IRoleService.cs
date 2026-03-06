using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Rbac;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Rbac;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IRoleService
{
    Task<PagedResult<RoleResponse>> SearchAsync(RoleListQuery query, CancellationToken ct = default);
    Task<RoleResponse> GetByIdAsync(int id, CancellationToken ct = default);
    Task<RoleResponse> CreateAsync(RoleRequestDto dto, CancellationToken ct = default);
    Task<RoleResponse> UpdateAsync(int id, RoleRequestDto dto, CancellationToken ct = default);
    Task<RoleResponse> ChangeStatusAsync(int id, ChangeEntityStatusRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, string? reason, CancellationToken ct = default);
}