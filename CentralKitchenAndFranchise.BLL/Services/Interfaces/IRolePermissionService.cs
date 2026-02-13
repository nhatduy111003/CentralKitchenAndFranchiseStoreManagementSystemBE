using CentralKitchenAndFranchise.DTO.Responses.Rbac;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IRolePermissionService
{
    Task<List<RolePermissionResponse>> GetPermissionsByRoleAsync(int roleId, CancellationToken ct = default);
    Task AssignToRoleAsync(int roleId, int permissionId, CancellationToken ct = default);
    Task RemovePermissionAsync(int roleId, int permissionId, CancellationToken ct = default);
}