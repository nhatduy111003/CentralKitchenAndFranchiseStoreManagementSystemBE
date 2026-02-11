using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces
{
    public interface IPermissionService
    {
        Task<List<PermissionDto>> GetAllAsync();

        Task<PermissionDto?> GetByIdAsync(int id);

        Task<PermissionDto> CreateAsync(CreatePermissionDto dto);

        Task<bool> UpdateAsync(int id, CreatePermissionDto dto);

        Task<bool> DeleteAsync(int id);
    }

}
