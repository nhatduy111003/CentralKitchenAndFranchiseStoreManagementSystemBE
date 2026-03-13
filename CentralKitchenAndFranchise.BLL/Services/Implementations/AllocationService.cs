using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Allocations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations
{
    public class AllocationService : IAllocationService
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUserService _current;
        public AllocationService(AppDbContext db, ICurrentUserService current)
        {
            _db = db;
            _current = current;
        }

        public async Task<int> CreateAsync(CreateAllocationDto dto)
        {
            RequireSupplyRoles();
            var allocation = new Allocation
            {
                DemandAggregationId = dto.DemandAggregationId,
                CreatedAt = DateTime.UtcNow
            };

            _db.Allocations.Add(allocation);
            await _db.SaveChangesAsync();
            return allocation.AllocationId;
            
        }

        public async Task AddItemAsync(int allocationId, AddAllocationItemDto dto)
        {
            RequireSupplyRoles();

            var franchiseExists = await _db.Franchises
        .AnyAsync(x => x.FranchiseId == dto.FranchiseId);

            if (!franchiseExists)
                throw new Exception("Franchise không tồn tại");

            var productExists = await _db.Products
                .AnyAsync(x => x.ProductId == dto.ProductId);

            if (!productExists)
                throw new Exception("Product không tồn tại");

            if (dto.Quantity <= 0)
                throw new Exception("Số lượng phải lớn hơn 0");
            var item = new AllocationItem
            {
                AllocationId = allocationId,
                FranchiseId = dto.FranchiseId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity
            };

            _db.AllocationItems.Add(item);
            await _db.SaveChangesAsync();
        }

        public async Task<Allocation?> GetAsync(int id)
        {
            RequireSupplyRoles();

            return await _db.Allocations
                .Include(x => x.AllocationItems)
                .ThenInclude(x => x.Franchise)
                .Include(x => x.AllocationItems)
                .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(x => x.AllocationId == id);
        }

        public async Task<List<Allocation>> GetAllAsync()
        {
            RequireSupplyRoles();

            return await _db.Allocations
                .Include(x => x.AllocationItems)
                .ToListAsync();
        }

        public async Task UpdateItemAsync(int itemId, decimal quantity)
        {
            RequireSupplyRoles();

            var item = await _db.AllocationItems.FindAsync(itemId);
            if (item == null) throw new Exception("Allocation item not found");

            item.Quantity = quantity;
            await _db.SaveChangesAsync();
        }

        public async Task RemoveItemAsync(int itemId)
        {
            RequireSupplyRoles();

            var item = await _db.AllocationItems.FindAsync(itemId);
            if (item == null) return;

            _db.AllocationItems.Remove(item);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int allocationId)
        {
            RequireSupplyRoles();

            var alloc = await _db.Allocations.FindAsync(allocationId);
            if (alloc == null) return;

            _db.Allocations.Remove(alloc);
            await _db.SaveChangesAsync();
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
