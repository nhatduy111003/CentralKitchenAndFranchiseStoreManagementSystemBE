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

public class RoleService : IRoleService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public RoleService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PagedResult<RoleResponse>> SearchAsync(RoleListQuery query, CancellationToken ct = default)
    {
        RequireAdmin();

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        if (pageSize > 200) pageSize = 200;

        var status = (query.Status ?? "ACTIVE").Trim().ToUpperInvariant();
        if (status is not ("ACTIVE" or "INACTIVE" or "ALL"))
            throw new ArgumentException("status must be ACTIVE, INACTIVE, or ALL.");

        var sortBy = (query.SortBy ?? "name").Trim().ToLowerInvariant();
        var sortDir = (query.SortDir ?? "asc").Trim().ToLowerInvariant();
        if (sortDir is not ("asc" or "desc"))
            throw new ArgumentException("sortDir must be asc or desc.");

        IQueryable<Role> q = _db.Roles.AsNoTracking();

        if (status != "ALL")
            q = q.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            q = q.Where(x => EF.Functions.ILike(x.Name, $"%{term}%"));
        }

        var total = await q.CountAsync(ct);
        q = ApplySort(q, sortBy, sortDir);

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<RoleResponse>.Create(
            items: items.Select(ToDto).ToList(),
            page: page,
            pageSize: pageSize,
            totalItems: total
        );
    }

    public async Task<RoleResponse> GetByIdAsync(int id, CancellationToken ct = default)
    {
        RequireAdmin();

        var entity = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.RoleId == id, ct);
        if (entity is null) throw new KeyNotFoundException($"Role {id} not found.");
        return ToDto(entity);
    }

    public async Task<RoleResponse> CreateAsync(RoleRequestDto dto, CancellationToken ct = default)
    {
        RequireAdmin();

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Name is required.");

        var now = DateTime.UtcNow;

        var entity = new Role
        {
            Name = dto.Name.Trim(),
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Roles.Add(entity);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Role name already exists.");
        }

        await AddAuditAsync(
            action: "ROLE_CREATE",
            entityName: "Role",
            entityId: entity.RoleId,
            oldObj: null,
            newObj: entity,
            reason: null,
            ct: ct
        );

        return ToDto(entity);
    }

    public async Task<RoleResponse> UpdateAsync(int id, RoleRequestDto dto, CancellationToken ct = default)
    {
        RequireAdmin();

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Name is required.");

        var entity = await _db.Roles.FirstOrDefaultAsync(x => x.RoleId == id, ct);
        if (entity is null) throw new KeyNotFoundException($"Role {id} not found.");

        var old = new { entity.RoleId, entity.Name, entity.Status, entity.CreatedAt, entity.UpdatedAt };

        entity.Name = dto.Name.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Role name already exists.");
        }

        await AddAuditAsync(
            action: "ROLE_UPDATE",
            entityName: "Role",
            entityId: entity.RoleId,
            oldObj: old,
            newObj: entity,
            reason: null,
            ct: ct
        );

        return ToDto(entity);
    }

    public async Task<RoleResponse> ChangeStatusAsync(int id, ChangeEntityStatusRequest request, CancellationToken ct = default)
    {
        RequireAdmin();

        if (string.IsNullOrWhiteSpace(request.Status))
            throw new ArgumentException("Status is required.");

        var newStatus = request.Status.Trim().ToUpperInvariant();
        if (newStatus is not ("ACTIVE" or "INACTIVE"))
            throw new ArgumentException("status must be ACTIVE or INACTIVE.");

        var entity = await _db.Roles.FirstOrDefaultAsync(x => x.RoleId == id, ct);
        if (entity is null) throw new KeyNotFoundException($"Role {id} not found.");

        if (entity.Status == newStatus) return ToDto(entity);

        if (newStatus == "INACTIVE")
        {
            var anyActiveUsers = await _db.Users.AsNoTracking()
                .AnyAsync(x => x.RoleId == id && x.Status == "ACTIVE", ct);
            if (anyActiveUsers)
                throw new InvalidOperationException("Cannot deactivate a role that is assigned to active users.");
        }

        var old = new { entity.RoleId, entity.Name, entity.Status, entity.CreatedAt, entity.UpdatedAt };

        entity.Status = newStatus;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "ROLE_STATUS_CHANGE",
            entityName: "Role",
            entityId: entity.RoleId,
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

    private static IQueryable<Role> ApplySort(IQueryable<Role> q, string sortBy, string sortDir)
    {
        var desc = sortDir == "desc";

        return sortBy switch
        {
            "id" => desc ? q.OrderByDescending(x => x.RoleId) : q.OrderBy(x => x.RoleId),
            "createdat" => desc ? q.OrderByDescending(x => x.CreatedAt) : q.OrderBy(x => x.CreatedAt),
            "updatedat" => desc ? q.OrderByDescending(x => x.UpdatedAt) : q.OrderBy(x => x.UpdatedAt),
            "name" => desc ? q.OrderByDescending(x => x.Name) : q.OrderBy(x => x.Name),
            _ => throw new ArgumentException("sortBy must be one of: id, name, createdAt, updatedAt")
        };
    }

    private static RoleResponse ToDto(Role x)
        => new()
        {
            Id = x.RoleId,
            Name = x.Name,
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