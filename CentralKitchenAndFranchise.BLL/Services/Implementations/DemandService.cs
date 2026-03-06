using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Demands;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations
{
    public class DemandService : IDemandService
    {
        private readonly AppDbContext _db;

        public DemandService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<int> CreateAsync(CreateDemandDto dto)
        {
            var demand = new DemandAggregation
            {
                PlanDate = dto.PlanDate,
                CreatedAt = DateTime.UtcNow
            };

            _db.DemandAggregations.Add(demand);
            await _db.SaveChangesAsync();
            return demand.DemandAggregationId;
        }

        public async Task AddItemAsync(int demandId, AddDemandItemDto dto)
        {
            var item = new DemandItem
            {
                DemandAggregationId = demandId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity
            };

            _db.DemandItems.Add(item);
            await _db.SaveChangesAsync();
        }

        public async Task<DemandAggregation?> GetAsync(int id)
        {
            return await _db.DemandAggregations
                .Include(x => x.DemandItems)
                .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(x => x.DemandAggregationId == id);
        }

        public async Task<List<DemandAggregation>> GetAllAsync()
        {
            return await _db.DemandAggregations
                .Include(x => x.DemandItems)
                .ToListAsync();
        }
    }

}
