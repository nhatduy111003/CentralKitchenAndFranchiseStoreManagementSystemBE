using CentralKitchenAndFranchise.DTO.Requests.Franchise;
using CentralKitchenAndFranchise.DTO.Responses.Franchise;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces
{
    public interface IFranchiseService
    {
        Task<List<FranchiseResponseDto>> GetAllAsync();
        Task<FranchiseResponseDto?> GetByIdAsync(int id);

        Task<int> CreateAsync(FranchiseCreateDto dto);
        Task<bool> UpdateAsync(int id, FranchiseUpdateDto dto);

        Task<bool> DeleteAsync(int id);
    }

}
