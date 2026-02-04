using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Allocations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces
{
    public interface IAllocationService
    {
        Task<int> CreateAsync(CreateAllocationDto dto);
        Task AddItemAsync(int allocationId, AddAllocationItemDto dto);

        Task<Allocation?> GetAsync(int id);
        Task<List<Allocation>> GetAllAsync();

        Task UpdateItemAsync(int itemId, decimal quantity);
        Task RemoveItemAsync(int itemId);

        Task DeleteAsync(int allocationId);
    }


}
