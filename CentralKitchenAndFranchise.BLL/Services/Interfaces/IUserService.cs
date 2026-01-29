using CentralKitchenAndFranchise.DTO.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces
{
    public interface IUserService
    {
        Task<List<UserDto>> GetAllAsync();
        Task<UserDto?> GetByIdAsync(int userId);
        Task<UserDto> CreateAsync(CreateUserRequestDto dto);
        Task<bool> UpdateAsync(int userId, UpdateUserRequestDto dto);
        Task<bool> DeleteAsync(int userId);
    }
}
