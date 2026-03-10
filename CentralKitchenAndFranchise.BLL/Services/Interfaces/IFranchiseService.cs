using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces
{
    public interface IFranchiseService
    {
        Task<List<FranchiseDto>> GetAllAsync();
        Task<FranchiseDto?> GetByIdAsync(int id);
        Task<int> CreateAsync(FranchiseCreateDto dto);
        Task<bool> UpdateAsync(int id, FranchiseCreateDto dto);
        Task<bool> DeleteAsync(int id);
    }

}
