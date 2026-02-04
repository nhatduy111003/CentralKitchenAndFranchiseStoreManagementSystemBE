using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Demands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces
{
    public interface IDemandService
    {
        Task<int> CreateAsync(CreateDemandDto dto);
        Task AddItemAsync(int demandId, AddDemandItemDto dto);
        Task<DemandAggregation?> GetAsync(int id);
        Task<List<DemandAggregation>> GetAllAsync();
    }



}
