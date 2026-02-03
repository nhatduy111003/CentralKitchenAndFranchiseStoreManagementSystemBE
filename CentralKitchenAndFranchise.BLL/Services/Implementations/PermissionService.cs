using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Requests;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations
{
    public class PermissionService : IPermissionService
    {
        private readonly AppDbContext _context;

        public PermissionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<PermissionDto>> GetAllAsync()
        {
            return await _context.Permissions
                .Select(p => new PermissionDto
                {
                    PermissionId = p.PermissionId,
                    Code = p.Code,
                    Description = p.Description
                })
                .ToListAsync();
        }

        public async Task<PermissionDto?> GetByIdAsync(int id)
        {
            var p = await _context.Permissions.FindAsync(id);

            if (p == null) return null;

            return new PermissionDto
            {
                PermissionId = p.PermissionId,
                Code = p.Code,
                Description = p.Description
            };
        }

        public async Task<PermissionDto> CreateAsync(CreatePermissionDto dto)
        {
            var entity = new Permission
            {
                Code = dto.Code,
                Name = dto.Name,
                GroupName = dto.GroupName,
                Description = dto.Description
            };

            _context.Permissions.Add(entity);
            await _context.SaveChangesAsync();

            return new PermissionDto
            {
                PermissionId = entity.PermissionId,
                Code = entity.Code
            };
        }

        public async Task<bool> UpdateAsync(int id, CreatePermissionDto dto)
        {
            var p = await _context.Permissions.FindAsync(id);
            if (p == null) return false;

            p.Code = dto.Code;
            p.Description = dto.Description;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var p = await _context.Permissions.FindAsync(id);
            if (p == null) return false;

            // remove role_permission first
            var rps = _context.RolePermissions
                .Where(r => r.PermissionId == id);

            _context.RolePermissions.RemoveRange(rps);
            _context.Permissions.Remove(p);

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
