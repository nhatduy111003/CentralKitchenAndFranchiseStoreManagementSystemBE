using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.SystemSettings;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.SystemSettings;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class SystemSettingService : ISystemSettingService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public SystemSettingService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PagedResult<SystemSettingResponse>> SearchAsync(SystemSettingListQuery query, CancellationToken ct = default)
    {
        RequireAdminOnly();

        query ??= new SystemSettingListQuery();
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        if (pageSize > 200) pageSize = 200;

        var sortBy = (query.SortBy ?? "key").Trim().ToLowerInvariant();
        var sortDir = (query.SortDir ?? "asc").Trim().ToLowerInvariant();
        if (sortDir is not ("asc" or "desc"))
            throw new ArgumentException("sortDir must be asc or desc.");

        IQueryable<SystemSetting> q = _db.SystemSettings.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            q = q.Where(x =>
                EF.Functions.ILike(x.Key, $"%{term}%") ||
                (x.Description != null && EF.Functions.ILike(x.Description, $"%{term}%")));
        }

        var total = await q.CountAsync(ct);

        q = ApplySort(q, sortBy, sortDir);

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<SystemSettingResponse>.Create(
            items: items.Select(Map).ToList(),
            totalItems: total,
            page: page,
            pageSize: pageSize);
    }

    public async Task<SystemSettingResponse> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        RequireAdminOnly();

        key = NormalizeKey(key);

        var entity = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, ct);
        if (entity is null)
            throw new KeyNotFoundException($"SystemSetting '{key}' not found.");

        return Map(entity);
    }

    public async Task<int> CreateAsync(SystemSettingRequest req, CancellationToken ct = default)
    {
        RequireAdminOnly();
        req = req ?? throw new ArgumentNullException(nameof(req));

        var key = NormalizeKey(req.Key);

        var exists = await _db.SystemSettings.AsNoTracking().AnyAsync(x => x.Key == key, ct);
        if (exists)
            throw new InvalidOperationException($"SystemSetting '{key}' already exists.");

        var entity = new SystemSetting
        {
            Key = key,
            Value = req.Value.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim()
        };

        await _db.SystemSettings.AddAsync(entity, ct);
        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            Action = "CREATE",
            EntityName = nameof(SystemSetting),
            EntityId = null,
            NewDataJson = JsonSerializer.Serialize(new { entity.Key, entity.Value, entity.Description }),
            Reason = "Create system setting",
            CreatedAt = DateTime.UtcNow
        }, ct);

        await _db.SaveChangesAsync(ct);
        return entity.SystemSettingId;
    }

    public async Task UpdateAsync(string key, SystemSettingRequest req, CancellationToken ct = default)
    {
        RequireAdminOnly();
        req = req ?? throw new ArgumentNullException(nameof(req));
        key = NormalizeKey(key);

        var entity = await _db.SystemSettings.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (entity is null)
            throw new KeyNotFoundException($"SystemSetting '{key}' not found.");

        var old = new { entity.Key, entity.Value, entity.Description };

        // Key immutable (update-by-key)
        entity.Value = req.Value.Trim();
        entity.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            Action = "UPDATE",
            EntityName = nameof(SystemSetting),
            EntityId = entity.SystemSettingId,
            OldDataJson = JsonSerializer.Serialize(old),
            NewDataJson = JsonSerializer.Serialize(new { entity.Key, entity.Value, entity.Description }),
            Reason = "Update system setting",
            CreatedAt = DateTime.UtcNow
        }, ct);

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        RequireAdminOnly();
        key = NormalizeKey(key);

        var entity = await _db.SystemSettings.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (entity is null)
            throw new KeyNotFoundException($"SystemSetting '{key}' not found.");

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            Action = "DELETE",
            EntityName = nameof(SystemSetting),
            EntityId = entity.SystemSettingId,
            OldDataJson = JsonSerializer.Serialize(new { entity.Key, entity.Value, entity.Description }),
            Reason = "Delete system setting",
            CreatedAt = DateTime.UtcNow
        }, ct);

        _db.SystemSettings.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("key is required.");
        return key.Trim();
    }

    private static IQueryable<SystemSetting> ApplySort(IQueryable<SystemSetting> q, string sortBy, string sortDir)
    {
        var desc = sortDir == "desc";
        return (sortBy) switch
        {
            "key" => desc ? q.OrderByDescending(x => x.Key) : q.OrderBy(x => x.Key),
            "createdat" => desc ? q.OrderByDescending(x => x.CreatedAt) : q.OrderBy(x => x.CreatedAt),
            "updatedat" => desc ? q.OrderByDescending(x => x.UpdatedAt) : q.OrderBy(x => x.UpdatedAt),
            _ => throw new ArgumentException("sortBy must be key, createdAt, or updatedAt.")
        };
    }

    private static SystemSettingResponse Map(SystemSetting x) => new()
    {
        Id = x.SystemSettingId,
        Key = x.Key,
        Value = x.Value,
        Description = x.Description,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };

    private void RequireAdminOnly()
    {
        if (!_current.IsInRole(RoleNames.Admin))
            throw new UnauthorizedAccessException("Only Admin can manage system settings.");
    }
}