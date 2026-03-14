using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Allocations;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations
{
    public class AllocationService : IAllocationService
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUserService _current;
        private readonly IFranchiseAccessService _access;

        public AllocationService(
            AppDbContext db,
            ICurrentUserService current,
            IFranchiseAccessService access)
        {
            _db = db;
            _current = current;
            _access = access;
        }

        public async Task<int> CreateAsync(CreateAllocationDto dto)
        {
            RequireSupplyRoles();

            var demand = await _db.DemandAggregations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.DemandAggregationId == dto.DemandAggregationId)
                ?? throw new Exception("Demand aggregation not found");

            await _access.EnsureCanAccessCentralKitchenAsync(demand.CentralKitchenId);

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

            if (dto.Quantity <= 0)
                throw new Exception("Số lượng phải lớn hơn 0");

            var allocation = await _db.Allocations
                .Include(x => x.DemandAggregation)
                .FirstOrDefaultAsync(x => x.AllocationId == allocationId)
                ?? throw new Exception("Allocation not found");

            await _access.EnsureCanAccessCentralKitchenAsync(allocation.DemandAggregation.CentralKitchenId);

            var franchise = await _db.Franchises
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.FranchiseId == dto.FranchiseId)
                ?? throw new Exception("Franchise không tồn tại");

            if (franchise.CentralKitchenId != allocation.DemandAggregation.CentralKitchenId)
                throw new Exception("Franchise does not belong to the demand's central kitchen.");

            var productExists = await _db.Products
                .AsNoTracking()
                .AnyAsync(x => x.ProductId == dto.ProductId);

            if (!productExists)
                throw new Exception("Product không tồn tại");

            var item = new AllocationItem
            {
                AllocationId = allocationId,
                FranchiseId = dto.FranchiseId,
                CentralKitchenId = allocation.DemandAggregation.CentralKitchenId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity
            };

            _db.AllocationItems.Add(item);
            await _db.SaveChangesAsync();
        }

        public async Task<Allocation?> GetAsync(int id)
        {
            RequireSupplyRoles();

            var allocation = await _db.Allocations
                .Include(x => x.DemandAggregation)
                .Include(x => x.AllocationItems)
                .ThenInclude(x => x.Franchise)
                .Include(x => x.AllocationItems)
                .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(x => x.AllocationId == id);

            if (allocation == null)
                return null;

            await _access.EnsureCanAccessCentralKitchenAsync(allocation.DemandAggregation.CentralKitchenId);
            return allocation;
        }

        public async Task<List<Allocation>> GetAllAsync()
        {
            RequireSupplyRoles();

            IQueryable<Allocation> query = _db.Allocations
                .Include(x => x.DemandAggregation)
                .Include(x => x.AllocationItems)
                .ThenInclude(x => x.Franchise)
                .Include(x => x.AllocationItems)
                .ThenInclude(x => x.Product);

            if (_current.IsInRole(RoleNames.SupplyCoordinator))
            {
                var assignedCentralKitchenId = await _access.GetCurrentAssignedCentralKitchenIdAsync();
                query = query.Where(x => x.DemandAggregation.CentralKitchenId == assignedCentralKitchenId);
            }

            return await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.AllocationId)
                .ToListAsync();
        }

        public async Task UpdateItemAsync(int itemId, decimal quantity)
        {
            RequireSupplyRoles();

            if (quantity <= 0)
                throw new Exception("Số lượng phải lớn hơn 0");

            var item = await _db.AllocationItems
                .Include(x => x.Allocation)
                .ThenInclude(x => x.DemandAggregation)
                .FirstOrDefaultAsync(x => x.AllocationItemId == itemId)
                ?? throw new Exception("Allocation item not found");

            await _access.EnsureCanAccessCentralKitchenAsync(item.Allocation.DemandAggregation.CentralKitchenId);

            item.CentralKitchenId = item.Allocation.DemandAggregation.CentralKitchenId;
            item.Quantity = quantity;
            await _db.SaveChangesAsync();
        }

        public async Task RemoveItemAsync(int itemId)
        {
            RequireSupplyRoles();

            var item = await _db.AllocationItems
                .Include(x => x.Allocation)
                .ThenInclude(x => x.DemandAggregation)
                .FirstOrDefaultAsync(x => x.AllocationItemId == itemId);

            if (item == null) return;

            await _access.EnsureCanAccessCentralKitchenAsync(item.Allocation.DemandAggregation.CentralKitchenId);

            _db.AllocationItems.Remove(item);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int allocationId)
        {
            RequireSupplyRoles();

            var alloc = await _db.Allocations
                .Include(x => x.DemandAggregation)
                .FirstOrDefaultAsync(x => x.AllocationId == allocationId);

            if (alloc == null) return;

            await _access.EnsureCanAccessCentralKitchenAsync(alloc.DemandAggregation.CentralKitchenId);

            _db.Allocations.Remove(alloc);
            await _db.SaveChangesAsync();
        }

        private void RequireSupplyRoles()
        {
            if (_current.IsInRole(RoleNames.Admin)) return;
            if (_current.IsInRole(RoleNames.Manager)) return;
            if (_current.IsInRole(RoleNames.SupplyCoordinator)) return;
            if (_current.IsInRole(RoleNames.KitchenStaff)) return;

            throw new ForbiddenAccessException("You do not have permission to access allocation.");
        }
    }
}
