using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Rbac;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Rbac;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IPermissionService
{
    Task<PagedResult<PermissionResponse>> SearchAsync(PermissionListQuery query, CancellationToken ct = default);
    Task<PermissionResponse> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PermissionResponse> CreateAsync(CreatePermissionDto dto, CancellationToken ct = default);
    Task<PermissionResponse> UpdateAsync(int id, CreatePermissionDto dto, CancellationToken ct = default);
    Task<PermissionResponse> ChangeStatusAsync(int id, ChangeEntityStatusRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, string? reason, CancellationToken ct = default);
}