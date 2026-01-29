using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Responses;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations
{
    public class RoleService : IRoleService
    {
        private readonly AppDbContext _context;

        public RoleService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<RoleDto>> GetAllAsync()
        {
            return await _context.Roles
                .OrderBy(r => r.RoleId)
                .Select(r => new RoleDto
                {
                    RoleId = r.RoleId,
                    Name = r.Name
                })
                .ToListAsync();
        }

        public async Task<RoleDto?> GetByIdAsync(int roleId)
        {
            return await _context.Roles
                .Where(r => r.RoleId == roleId)
                .Select(r => new RoleDto
                {
                    RoleId = r.RoleId,
                    Name = r.Name
                })
                .FirstOrDefaultAsync();
        }

        public async Task<RoleDto> CreateAsync(RoleRequestDto dto)
        {
            var role = new Role
            {
                Name = dto.Name
            };

            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            return new RoleDto
            {
                RoleId = role.RoleId,
                Name = role.Name
            };
        }

        public async Task<bool> UpdateAsync(int roleId, RoleRequestDto dto)
        {
            var role = await _context.Roles.FindAsync(roleId);
            if (role == null) return false;

            role.Name = dto.Name;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int roleId)
        {
            var role = await _context.Roles.FindAsync(roleId);
            if (role == null) return false;

            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
