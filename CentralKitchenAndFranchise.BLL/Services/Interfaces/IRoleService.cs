using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces
{
    public interface IRoleService
    {
        Task<List<RoleDto>> GetAllAsync();
        Task<RoleDto?> GetByIdAsync(int roleId);
        Task<RoleDto> CreateAsync(RoleRequestDto dto);
        Task<bool> UpdateAsync(int roleId, RoleRequestDto dto);
        Task<bool> DeleteAsync(int roleId);
    }
}
