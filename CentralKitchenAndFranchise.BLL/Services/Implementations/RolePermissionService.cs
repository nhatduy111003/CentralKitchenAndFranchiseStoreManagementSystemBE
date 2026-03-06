using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Responses.Rbac;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class RolePermissionService : IRolePermissionService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public RolePermissionService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<RolePermissionResponse>> GetPermissionsByRoleAsync(int roleId, CancellationToken ct = default)
    {
        RequireAdmin();

        var role = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.RoleId == roleId, ct);
        if (role is null) throw new KeyNotFoundException($"Role {roleId} not found.");

        return await _db.RolePermissions.AsNoTracking()
            .Where(x => x.RoleId == roleId)
            .OrderBy(x => x.Permission.GroupName)
            .ThenBy(x => x.Permission.Code)
            .Select(x => new RolePermissionResponse
            {
                RoleId = x.RoleId,
                PermissionId = x.PermissionId,
                PermissionCode = x.Permission.Code,
                PermissionName = x.Permission.Name,
                GroupName = x.Permission.GroupName
            })
            .ToListAsync(ct);
    }

    public async Task AssignToRoleAsync(int roleId, int permissionId, CancellationToken ct = default)
    {
        RequireAdmin();

        var role = await _db.Roles.FirstOrDefaultAsync(x => x.RoleId == roleId, ct);
        if (role is null) throw new KeyNotFoundException($"Role {roleId} not found.");
        if (role.Status != "ACTIVE") throw new InvalidOperationException("Role must be ACTIVE.");

        var perm = await _db.Permissions.FirstOrDefaultAsync(x => x.PermissionId == permissionId, ct);
        if (perm is null) throw new KeyNotFoundException($"Permission {permissionId} not found.");
        if (perm.Status != "ACTIVE") throw new InvalidOperationException("Permission must be ACTIVE.");

        var exists = await _db.RolePermissions.AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, ct);
        if (exists) return; // idempotent

        var rp = new RolePermission { RoleId = roleId, PermissionId = permissionId };
        _db.RolePermissions.Add(rp);
        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "ROLE_PERMISSION_ASSIGN",
            entityName: "RolePermission",
            entityId: 0,
            oldObj: null,
            newObj: new { RoleId = roleId, PermissionId = permissionId },
            reason: null,
            ct: ct
        );
    }

    public async Task RemovePermissionAsync(int roleId, int permissionId, CancellationToken ct = default)
    {
        RequireAdmin();

        var rp = await _db.RolePermissions.FirstOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, ct);
        if (rp is null) throw new KeyNotFoundException("Role permission mapping not found.");

        _db.RolePermissions.Remove(rp);
        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "ROLE_PERMISSION_REMOVE",
            entityName: "RolePermission",
            entityId: 0,
            oldObj: new { RoleId = roleId, PermissionId = permissionId },
            newObj: null,
            reason: null,
            ct: ct
        );
    }

    private void RequireAdmin()
    {
        if (!_current.IsInRole(RoleNames.Admin))
            throw new ForbiddenAccessException("Admin role required.");
    }

    private async Task AddAuditAsync(string action, string entityName, int entityId, object? oldObj, object? newObj, string? reason, CancellationToken ct)
    {
        var audit = new AuditLog
        {
            UserId = _current.UserId,
            FranchiseId = null,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldDataJson = oldObj == null ? null : JsonSerializer.Serialize(oldObj),
            NewDataJson = newObj == null ? null : JsonSerializer.Serialize(newObj),
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        };

        _db.AuditLogs.Add(audit);
        await _db.SaveChangesAsync(ct);
    }
}