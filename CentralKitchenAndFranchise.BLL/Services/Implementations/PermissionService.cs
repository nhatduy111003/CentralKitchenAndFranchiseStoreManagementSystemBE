using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Rbac;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Rbac;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public PermissionService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PagedResult<PermissionResponse>> SearchAsync(PermissionListQuery query, CancellationToken ct = default)
    {
        RequireAdmin();

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        if (pageSize > 200) pageSize = 200;

        var status = (query.Status ?? "ACTIVE").Trim().ToUpperInvariant();
        if (status is not ("ACTIVE" or "INACTIVE" or "ALL"))
            throw new ArgumentException("status must be ACTIVE, INACTIVE, or ALL.");

        var sortBy = (query.SortBy ?? "code").Trim().ToLowerInvariant();
        var sortDir = (query.SortDir ?? "asc").Trim().ToLowerInvariant();
        if (sortDir is not ("asc" or "desc"))
            throw new ArgumentException("sortDir must be asc or desc.");

        IQueryable<Permission> q = _db.Permissions.AsNoTracking();

        if (status != "ALL")
            q = q.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            q = q.Where(x =>
                EF.Functions.ILike(x.Code, $"%{term}%") ||
                EF.Functions.ILike(x.Name, $"%{term}%") ||
                EF.Functions.ILike(x.GroupName, $"%{term}%"));
        }

        var total = await q.CountAsync(ct);
        q = ApplySort(q, sortBy, sortDir);

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<PermissionResponse>.Create(
            items: items.Select(ToDto).ToList(),
            page: page,
            pageSize: pageSize,
            totalItems: total
        );
    }

    public async Task<PermissionResponse> GetByIdAsync(int id, CancellationToken ct = default)
    {
        RequireAdmin();

        var entity = await _db.Permissions.AsNoTracking().FirstOrDefaultAsync(x => x.PermissionId == id, ct);
        if (entity is null) throw new KeyNotFoundException($"Permission {id} not found.");
        return ToDto(entity);
    }

    public async Task<PermissionResponse> CreateAsync(CreatePermissionDto dto, CancellationToken ct = default)
    {
        RequireAdmin();

        ValidateCreateOrUpdate(dto);

        var now = DateTime.UtcNow;
        var entity = new Permission
        {
            Code = dto.Code.Trim(),
            Name = dto.Name.Trim(),
            GroupName = dto.GroupName.Trim(),
            Description = dto.Description.Trim(),
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Permissions.Add(entity);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Permission code already exists.");
        }

        await AddAuditAsync(
            action: "PERMISSION_CREATE",
            entityName: "Permission",
            entityId: entity.PermissionId,
            oldObj: null,
            newObj: entity,
            reason: null,
            ct: ct
        );

        return ToDto(entity);
    }

    public async Task<PermissionResponse> UpdateAsync(int id, CreatePermissionDto dto, CancellationToken ct = default)
    {
        RequireAdmin();

        ValidateCreateOrUpdate(dto);

        var entity = await _db.Permissions.FirstOrDefaultAsync(x => x.PermissionId == id, ct);
        if (entity is null) throw new KeyNotFoundException($"Permission {id} not found.");

        var old = new
        {
            entity.PermissionId,
            entity.Code,
            entity.Name,
            entity.GroupName,
            entity.Description,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt
        };

        entity.Code = dto.Code.Trim();
        entity.Name = dto.Name.Trim();
        entity.GroupName = dto.GroupName.Trim();
        entity.Description = dto.Description.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Permission code already exists.");
        }

        await AddAuditAsync(
            action: "PERMISSION_UPDATE",
            entityName: "Permission",
            entityId: entity.PermissionId,
            oldObj: old,
            newObj: entity,
            reason: null,
            ct: ct
        );

        return ToDto(entity);
    }

    public async Task<PermissionResponse> ChangeStatusAsync(int id, ChangeEntityStatusRequest request, CancellationToken ct = default)
    {
        RequireAdmin();

        if (string.IsNullOrWhiteSpace(request.Status))
            throw new ArgumentException("Status is required.");

        var newStatus = request.Status.Trim().ToUpperInvariant();
        if (newStatus is not ("ACTIVE" or "INACTIVE"))
            throw new ArgumentException("status must be ACTIVE or INACTIVE.");

        var entity = await _db.Permissions.FirstOrDefaultAsync(x => x.PermissionId == id, ct);
        if (entity is null) throw new KeyNotFoundException($"Permission {id} not found.");

        if (entity.Status == newStatus) return ToDto(entity);

        var old = new
        {
            entity.PermissionId,
            entity.Code,
            entity.Name,
            entity.GroupName,
            entity.Description,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt
        };

        entity.Status = newStatus;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "PERMISSION_STATUS_CHANGE",
            entityName: "Permission",
            entityId: entity.PermissionId,
            oldObj: old,
            newObj: entity,
            reason: request.Reason,
            ct: ct
        );

        return ToDto(entity);
    }

    public async Task DeleteAsync(int id, string? reason, CancellationToken ct = default)
    {
        await ChangeStatusAsync(id, new ChangeEntityStatusRequest { Status = "INACTIVE", Reason = reason }, ct);
    }

    private void RequireAdmin()
    {
        if (!_current.IsInRole(RoleNames.Admin))
            throw new ForbiddenAccessException("Admin role required.");
    }

    private static void ValidateCreateOrUpdate(CreatePermissionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code)) throw new ArgumentException("Code is required.");
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Name is required.");
        if (string.IsNullOrWhiteSpace(dto.GroupName)) throw new ArgumentException("GroupName is required.");
        if (string.IsNullOrWhiteSpace(dto.Description)) throw new ArgumentException("Description is required.");
    }

    private static IQueryable<Permission> ApplySort(IQueryable<Permission> q, string sortBy, string sortDir)
    {
        var desc = sortDir == "desc";

        return sortBy switch
        {
            "id" => desc ? q.OrderByDescending(x => x.PermissionId) : q.OrderBy(x => x.PermissionId),
            "createdat" => desc ? q.OrderByDescending(x => x.CreatedAt) : q.OrderBy(x => x.CreatedAt),
            "updatedat" => desc ? q.OrderByDescending(x => x.UpdatedAt) : q.OrderBy(x => x.UpdatedAt),
            "code" => desc ? q.OrderByDescending(x => x.Code) : q.OrderBy(x => x.Code),
            "name" => desc ? q.OrderByDescending(x => x.Name) : q.OrderBy(x => x.Name),
            "groupname" => desc ? q.OrderByDescending(x => x.GroupName) : q.OrderBy(x => x.GroupName),
            _ => throw new ArgumentException("sortBy must be one of: id, code, name, groupName, createdAt, updatedAt")
        };
    }

    private static PermissionResponse ToDto(Permission x)
        => new()
        {
            Id = x.PermissionId,
            Code = x.Code,
            Name = x.Name,
            GroupName = x.GroupName,
            Description = x.Description,
            Status = x.Status,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        };

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