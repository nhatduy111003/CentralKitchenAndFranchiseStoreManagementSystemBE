using CentralKitchenAndFranchise.DTO.Requests.Suppliers;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Suppliers;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface ISupplierService
{
    Task<PagedResult<SupplierResponse>> SearchAsync(SupplierListQuery query, CancellationToken ct = default);

    Task<SupplierResponse> GetByIdAsync(int id, CancellationToken ct = default);
    Task<SupplierResponse> CreateAsync(CreateSupplierRequest request, CancellationToken ct = default);
    Task<SupplierResponse> UpdateAsync(int id, UpdateSupplierRequest request, CancellationToken ct = default);
    Task<SupplierResponse> ChangeStatusAsync(int id, ChangeSupplierStatusRequest request, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default); // = deactivate
}
