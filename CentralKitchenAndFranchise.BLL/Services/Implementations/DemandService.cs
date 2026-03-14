using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Demands;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations
{
    public class DemandService : IDemandService
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUserService _current;
        private readonly IFranchiseAccessService _access;

        public DemandService(
            AppDbContext db,
            ICurrentUserService current,
            IFranchiseAccessService access)
        {
            _db = db;
            _current = current;
            _access = access;
        }

        public async Task<int> CreateAsync(CreateDemandDto dto)
        {
            RequireSupplyRoles();

            var centralKitchenId = await ResolveTargetCentralKitchenIdAsync(dto);

            var demand = new DemandAggregation
            {
                PlanDate = dto.PlanDate,
                CentralKitchenId = centralKitchenId,
                CreatedAt = DateTime.UtcNow
            };

            _db.DemandAggregations.Add(demand);
            await _db.SaveChangesAsync();
            return demand.DemandAggregationId;
        }

        public async Task AddItemAsync(int demandId, AddDemandItemDto dto)
        {
            RequireSupplyRoles();

            if (dto.Quantity <= 0)
                throw new Exception("Số lượng phải lớn hơn 0");

            var demand = await _db.DemandAggregations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.DemandAggregationId == demandId)
                ?? throw new Exception("Demand aggregation not found");

            await _access.EnsureCanAccessCentralKitchenAsync(demand.CentralKitchenId);

            var productExists = await _db.Products
                .AsNoTracking()
                .AnyAsync(x => x.ProductId == dto.ProductId);

            if (!productExists)
                throw new Exception("Product không tồn tại");

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

            var demand = await _db.DemandAggregations
                .Include(x => x.DemandItems)
                .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(x => x.DemandAggregationId == id);

            if (demand == null)
                return null;

            await _access.EnsureCanAccessCentralKitchenAsync(demand.CentralKitchenId);
            return demand;
        }

        public async Task<List<DemandAggregation>> GetAllAsync()
        {
            RequireSupplyRoles();

            IQueryable<DemandAggregation> query = _db.DemandAggregations
                .Include(x => x.DemandItems)
                .ThenInclude(x => x.Product);

            if (_current.IsInRole(RoleNames.SupplyCoordinator))
            {
                var assignedCentralKitchenId = await _access.GetCurrentAssignedCentralKitchenIdAsync();
                query = query.Where(x => x.CentralKitchenId == assignedCentralKitchenId);
            }

            return await query
                .OrderByDescending(x => x.PlanDate)
                .ThenByDescending(x => x.DemandAggregationId)
                .ToListAsync();
        }

        private async Task<int> ResolveTargetCentralKitchenIdAsync(CreateDemandDto dto)
        {
            int centralKitchenId;

            if (_current.IsInRole(RoleNames.SupplyCoordinator))
            {
                centralKitchenId = await _access.GetCurrentAssignedCentralKitchenIdAsync();

                if (dto.CentralKitchenId.HasValue && dto.CentralKitchenId.Value != centralKitchenId)
                    throw new ForbiddenAccessException("You do not have access to the requested central kitchen.");
            }
            else
            {
                if (!dto.CentralKitchenId.HasValue || dto.CentralKitchenId.Value <= 0)
                    throw new Exception("CentralKitchenId is required.");

                centralKitchenId = dto.CentralKitchenId.Value;
            }

            var centralKitchenExists = await _db.CentralKitchens
                .AsNoTracking()
                .AnyAsync(x => x.CentralKitchenId == centralKitchenId);

            if (!centralKitchenExists)
                throw new Exception("Central kitchen không tồn tại");

            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId);
            return centralKitchenId;
        }

        private void RequireSupplyRoles()
        {
            if (_current.IsInRole(RoleNames.Admin)) return;
            if (_current.IsInRole(RoleNames.Manager)) return;
            if (_current.IsInRole(RoleNames.SupplyCoordinator)) return;
            if (_current.IsInRole(RoleNames.KitchenStaff)) return;

            throw new ForbiddenAccessException("You do not have permission to access demand aggregation.");
        }
    }
}
