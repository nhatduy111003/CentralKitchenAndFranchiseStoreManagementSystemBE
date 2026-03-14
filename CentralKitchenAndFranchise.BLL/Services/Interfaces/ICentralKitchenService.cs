using CentralKitchenAndFranchise.DTO.Requests.CentralKitchens;
using CentralKitchenAndFranchise.DTO.Responses.CentralKitchens;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface ICentralKitchenService
{
    Task<List<CentralKitchenResponseDto>> GetAllAsync();
    Task<CentralKitchenResponseDto?> GetByIdAsync(int id);

    Task<int> CreateAsync(CentralKitchenCreateDto dto);
    Task<bool> UpdateAsync(int id, CentralKitchenUpdateDto dto);

    Task<bool> DeleteAsync(int id);
}