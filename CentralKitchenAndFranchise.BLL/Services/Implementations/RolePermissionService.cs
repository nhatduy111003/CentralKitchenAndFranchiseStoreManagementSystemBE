using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations
{
    public class RolePermissionService : IRolePermissionService
    {
        private readonly AppDbContext _context;

        public RolePermissionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddPermissionToRoleAsync(int roleId, int permissionId)
        {
            var exists = await _context.RolePermissions
                .AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionId);

            if (exists)
                throw new Exception("Permission already assigned");

            _context.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId
            });

            await _context.SaveChangesAsync();
        }

        public async Task RemovePermissionAsync(int roleId, int permissionId)
        {
            var rp = await _context.RolePermissions
                .FirstOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permissionId);

            if (rp == null)
                throw new Exception("Not found");

            _context.RolePermissions.Remove(rp);
            await _context.SaveChangesAsync();
        }

        public async Task<List<string>> GetPermissionsByRoleAsync(int roleId)
        {
            return await _context.RolePermissions
                .Where(x => x.RoleId == roleId)
                .Select(x => x.Permission.Code)
                .ToListAsync();
        }
        public async Task AssignToRoleAsync(int roleId, int permissionId)
        {
            // check role tồn tại
            var roleExists = await _context.Roles.AnyAsync(x => x.RoleId == roleId);
            if (!roleExists)
                throw new Exception("Role not found");

            // check permission tồn tại
            var permExists = await _context.Permissions.AnyAsync(x => x.PermissionId == permissionId);
            if (!permExists)
                throw new Exception("Permission not found");

            // check đã gán chưa
            var exists = await _context.RolePermissions
                .AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionId);

            if (exists)
                return;

            _context.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId
            });

            await _context.SaveChangesAsync();
        }
    }


}
