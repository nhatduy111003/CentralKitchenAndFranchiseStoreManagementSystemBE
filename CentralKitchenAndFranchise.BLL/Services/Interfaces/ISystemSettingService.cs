using CentralKitchenAndFranchise.DTO.Requests.SystemSettings;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.SystemSettings;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface ISystemSettingService
{
    Task<PagedResult<SystemSettingResponse>> SearchAsync(SystemSettingListQuery query, CancellationToken ct = default);
    Task<SystemSettingResponse> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<int> CreateAsync(SystemSettingRequest req, CancellationToken ct = default);
    Task UpdateAsync(string key, SystemSettingRequest req, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}