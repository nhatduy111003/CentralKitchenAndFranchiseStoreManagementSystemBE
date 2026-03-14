using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
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
        private readonly ICurrentUserService _current;
        public DemandService(AppDbContext db,ICurrentUserService current)
        {
            _db = db;
            _current = current;
        }

        public async Task<int> CreateAsync(CreateDemandDto dto)
        {
            RequireSupplyRoles();
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
            RequireSupplyRoles();

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
            RequireSupplyRoles();

            return await _db.DemandAggregations
                .Include(x => x.DemandItems)
                .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(x => x.DemandAggregationId == id);
        }

        public async Task<List<DemandAggregation>> GetAllAsync()
        {
            RequireSupplyRoles();

            return await _db.DemandAggregations
                .Include(x => x.DemandItems)
                .ToListAsync();
        }

        private void RequireSupplyRoles()
        {
            // Admin/Manager allowed for testing and supervision.
            if (_current.IsInRole(RoleNames.Admin)) return;
            if (_current.IsInRole(RoleNames.Manager)) return;
            if (_current.IsInRole(RoleNames.SupplyCoordinator)) return;

            throw new ForbiddenAccessException("You do not have permission to access store ordering.");
        }
    }

}
