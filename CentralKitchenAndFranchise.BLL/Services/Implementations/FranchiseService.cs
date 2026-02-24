using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Responses;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations
{
    public class FranchiseService : IFranchiseService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _current;

        public FranchiseService(AppDbContext context, ICurrentUserService current)
        {
            _context = context;
            _current = current;
        }

        public async Task<List<FranchiseDto>> GetAllAsync()
        {
            // Manager thấy tất cả franchises
            RequireAdminOrManager();

            return await _context.Franchises
                .AsNoTracking()
                .Select(f => new FranchiseDto
                {
                    FranchiseId = f.FranchiseId,
                    Name = f.Name,
                    Type = f.Type,
                    Status = f.Status,
                    Address = f.Address,
                    Location = f.Location
                })
                .ToListAsync();
        }

        public async Task<FranchiseDto?> GetByIdAsync(int id)
        {
            //  Manager thấy tất cả franchises
            RequireAdminOrManager();

            var f = await _context.Franchises
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.FranchiseId == id);

            if (f is null) return null;

            return new FranchiseDto
            {
                FranchiseId = f.FranchiseId,
                Name = f.Name,
                Type = f.Type,
                Status = f.Status,
                Address = f.Address,
                Location = f.Location,
                Latitude = f.Latitude,
                Longitude = f.Longitude,
             };
        }

        public async Task<int> CreateAsync(FranchiseCreateDto dto)
        {
            RequireAdminOnly();

            var franchise = new Franchise
            {
                Name = dto.Name,
                Type = dto.Type,
                Status = dto.Status,
                Address = dto.Address,
                Location = dto.Location,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
            };

            _context.Franchises.Add(franchise);
            await _context.SaveChangesAsync();

            return franchise.FranchiseId;
        }

        public async Task<bool> UpdateAsync(int id, FranchiseCreateDto dto)
        {
            RequireAdminOnly();

            var franchise = await _context.Franchises.FindAsync(id);
            if (franchise is null) return false;

            franchise.Name = dto.Name;
            franchise.Type = dto.Type;
            franchise.Status = dto.Status;
            franchise.Address = dto.Address;
            franchise.Location = dto.Location;
            franchise.Latitude = dto.Latitude;
            franchise.Longitude = dto.Longitude;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            RequireAdminOnly();

            var franchise = await _context.Franchises.FindAsync(id);
            if (franchise is null) return false;

            _context.Franchises.Remove(franchise);
            await _context.SaveChangesAsync();
            return true;
        }

        private void RequireAdminOrManager()
        {
            var role = _current.Role;
            if (role != RoleNames.Admin && role != RoleNames.Manager)
                throw new UnauthorizedAccessException("Only Admin/Manager can access franchises.");
        }

        private void RequireAdminOnly()
        {
            var role = _current.Role;
            if (role != RoleNames.Admin)
                throw new UnauthorizedAccessException("Only Admin can perform this action.");
        }
    }
}
