using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.CentralKitchens;
using CentralKitchenAndFranchise.DTO.Responses.CentralKitchens;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class CentralKitchenService : ICentralKitchenService
{
    private const string StatusActive = "ACTIVE";
    private const string StatusInactive = "INACTIVE";

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        StatusActive,
        StatusInactive
    };

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public CentralKitchenService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<CentralKitchenResponseDto>> GetAllAsync()
    {
        RequireAdminOrManager();

        var items = await _db.CentralKitchens
            .AsNoTracking()
            .Select(x => new CentralKitchenResponseDto
            {
                CentralKitchenId = x.CentralKitchenId,
                Name = x.Name,
                Status = x.Status,
                Address = x.Address,
                Location = x.Location,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                FranchiseCount = x.Franchises.Count,
                ActiveFranchiseCount = x.Franchises.Count(f => f.Status == StatusActive),
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return items;
    }

    public async Task<CentralKitchenResponseDto?> GetByIdAsync(int id)
    {
        RequireAdminOrManager();

        return await _db.CentralKitchens
            .AsNoTracking()
            .Where(x => x.CentralKitchenId == id)
            .Select(x => new CentralKitchenResponseDto
            {
                CentralKitchenId = x.CentralKitchenId,
                Name = x.Name,
                Status = x.Status,
                Address = x.Address,
                Location = x.Location,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                FranchiseCount = x.Franchises.Count,
                ActiveFranchiseCount = x.Franchises.Count(f => f.Status == StatusActive),
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .FirstOrDefaultAsync();
    }

    public async Task<int> CreateAsync(CentralKitchenCreateDto dto)
    {
        RequireAdminOrManager();
        ArgumentNullException.ThrowIfNull(dto);

        var normalizedName = NormalizeRequired(dto.Name, nameof(dto.Name));
        await EnsureNameUniqueAsync(normalizedName);

        var now = DateTime.UtcNow;

        var entity = new CentralKitchen
        {
            Name = normalizedName,
            Status = NormalizeStatus(dto.Status, StatusActive),
            Address = NormalizeNullable(dto.Address),
            Location = NormalizeNullable(dto.Location),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var tx = await _db.Database.BeginTransactionAsync();

        await _db.CentralKitchens.AddAsync(entity);
        await _db.SaveChangesAsync();

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            CentralKitchenId = entity.CentralKitchenId,
            Action = "CREATE",
            EntityName = nameof(CentralKitchen),
            EntityId = entity.CentralKitchenId,
            OldDataJson = null,
            NewDataJson = JsonSerializer.Serialize(ToAuditSnapshot(entity)),
            Reason = "Create central kitchen",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return entity.CentralKitchenId;
    }

    public async Task<bool> UpdateAsync(int id, CentralKitchenUpdateDto dto)
    {
        RequireAdminOrManager();
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await _db.CentralKitchens.FirstOrDefaultAsync(x => x.CentralKitchenId == id);
        if (entity is null) return false;

        var normalizedName = NormalizeRequired(dto.Name, nameof(dto.Name));
        await EnsureNameUniqueAsync(normalizedName, entity.CentralKitchenId);

        var old = ToAuditSnapshot(entity);

        entity.Name = normalizedName;
        entity.Status = NormalizeStatus(dto.Status, entity.Status);
        entity.Address = NormalizeNullable(dto.Address);
        entity.Location = NormalizeNullable(dto.Location);
        entity.Latitude = dto.Latitude;
        entity.Longitude = dto.Longitude;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            CentralKitchenId = entity.CentralKitchenId,
            Action = "UPDATE",
            EntityName = nameof(CentralKitchen),
            EntityId = entity.CentralKitchenId,
            OldDataJson = JsonSerializer.Serialize(old),
            NewDataJson = JsonSerializer.Serialize(ToAuditSnapshot(entity)),
            Reason = "Update central kitchen",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        RequireAdminOrManager();

        var entity = await _db.CentralKitchens.FirstOrDefaultAsync(x => x.CentralKitchenId == id);
        if (entity is null) return false;

        if (string.Equals(entity.Status, StatusInactive, StringComparison.OrdinalIgnoreCase))
            return true;

        var hasActiveFranchises = await _db.Franchises
            .AsNoTracking()
            .AnyAsync(x => x.CentralKitchenId == id && x.Status == StatusActive);

        if (hasActiveFranchises)
        {
            throw new InvalidOperationException(
                $"Cannot deactivate central kitchen {id} while active franchises still belong to it.");
        }

        var old = ToAuditSnapshot(entity);

        entity.Status = StatusInactive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            CentralKitchenId = entity.CentralKitchenId,
            Action = "DEACTIVATE",
            EntityName = nameof(CentralKitchen),
            EntityId = entity.CentralKitchenId,
            OldDataJson = JsonSerializer.Serialize(old),
            NewDataJson = JsonSerializer.Serialize(ToAuditSnapshot(entity)),
            Reason = "Deactivate central kitchen",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return true;
    }

    private async Task EnsureNameUniqueAsync(string name, int? excludeId = null)
    {
        var query = _db.CentralKitchens
            .AsNoTracking()
            .Where(x => x.Name.ToLower() == name.ToLower());

        if (excludeId.HasValue)
            query = query.Where(x => x.CentralKitchenId != excludeId.Value);

        var exists = await query.AnyAsync();
        if (exists)
            throw new InvalidOperationException($"Central kitchen name '{name}' already exists.");
    }

    private static object ToAuditSnapshot(CentralKitchen x) => new
    {
        x.CentralKitchenId,
        x.Name,
        x.Status,
        x.Address,
        x.Location,
        x.Latitude,
        x.Longitude
    };

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{fieldName} is required.", fieldName);

        return value.Trim();
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeStatus(string? status, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(status)
            ? fallback
            : status.Trim().ToUpperInvariant();

        if (!AllowedStatuses.Contains(normalized))
        {
            throw new InvalidOperationException(
                $"Invalid central kitchen status '{status}'. Allowed values: {string.Join(", ", AllowedStatuses)}.");
        }

        return normalized;
    }

    private void RequireAdminOrManager()
    {
        if (!_current.IsInRole(RoleNames.Admin) && !_current.IsInRole(RoleNames.Manager))
            throw new UnauthorizedAccessException("Only Admin/Manager can access central kitchens.");
    }

  
}