using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces
{
    public interface IRolePermissionService
    {
        Task AddPermissionToRoleAsync(int roleId, int permissionId);
        Task RemovePermissionAsync(int roleId, int permissionId);
        Task<List<string>> GetPermissionsByRoleAsync(int roleId);
        Task AssignToRoleAsync(int roleId, int permissionId);
    }

}
