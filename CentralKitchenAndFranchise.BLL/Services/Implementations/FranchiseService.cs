using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Franchise;
using CentralKitchenAndFranchise.DTO.Responses.Franchise;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class FranchiseService : IFranchiseService
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

    public FranchiseService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<FranchiseResponseDto>> GetAllAsync()
    {
        RequireAdminOrManager();

        var query = _db.Franchises
            .AsNoTracking()
            .Include(x => x.CentralKitchen)
            .OrderByDescending(x => x.CreatedAt)
            .AsQueryable();

        // TODO:
        // Nếu sau này chốt manager scope thật thì filter ở đây,
        // không để comment ở controller rồi service trả full data.

        var items = await query.ToListAsync();
        return items.Select(Map).ToList();
    }

    public async Task<FranchiseResponseDto?> GetByIdAsync(int id)
    {
        RequireAdminOrManager();

        var entity = await _db.Franchises
            .AsNoTracking()
            .Include(x => x.CentralKitchen)
            .FirstOrDefaultAsync(x => x.FranchiseId == id);

        return entity is null ? null : Map(entity);
    }

    public async Task<int> CreateAsync(FranchiseCreateDto dto)
    {
        RequireAdminOnly();
        ArgumentNullException.ThrowIfNull(dto);

        await EnsureCentralKitchenExistsAsync(dto.CentralKitchenId);

        var entity = new Franchise
        {
            CentralKitchenId = dto.CentralKitchenId,
            Name = NormalizeRequired(dto.Name, nameof(dto.Name)),
            Type = NormalizeRequired(dto.Type, nameof(dto.Type)),
            Status = NormalizeStatus(dto.Status, StatusActive),
            Address = NormalizeNullable(dto.Address),
            Location = NormalizeNullable(dto.Location),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude
        };

        await using var tx = await _db.Database.BeginTransactionAsync();

        await _db.Franchises.AddAsync(entity);
        await _db.SaveChangesAsync();

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            FranchiseId = entity.FranchiseId,
            CentralKitchenId = entity.CentralKitchenId,
            Action = "CREATE",
            EntityName = nameof(Franchise),
            EntityId = entity.FranchiseId,
            OldDataJson = null,
            NewDataJson = JsonSerializer.Serialize(ToAuditSnapshot(entity)),
            Reason = "Create franchise",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return entity.FranchiseId;
    }

    public async Task<bool> UpdateAsync(int id, FranchiseUpdateDto dto)
    {
        RequireAdminOnly();
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await _db.Franchises.FirstOrDefaultAsync(x => x.FranchiseId == id);
        if (entity is null) return false;

        var old = ToAuditSnapshot(entity);

        entity.Name = NormalizeRequired(dto.Name, nameof(dto.Name));
        entity.Type = NormalizeRequired(dto.Type, nameof(dto.Type));
        entity.Status = NormalizeStatus(dto.Status, entity.Status);
        entity.Address = NormalizeNullable(dto.Address);
        entity.Location = NormalizeNullable(dto.Location);
        entity.Latitude = dto.Latitude;
        entity.Longitude = dto.Longitude;

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            FranchiseId = entity.FranchiseId,
            CentralKitchenId = entity.CentralKitchenId,
            Action = "UPDATE",
            EntityName = nameof(Franchise),
            EntityId = entity.FranchiseId,
            OldDataJson = JsonSerializer.Serialize(old),
            NewDataJson = JsonSerializer.Serialize(ToAuditSnapshot(entity)),
            Reason = "Update franchise",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        RequireAdminOnly();

        var entity = await _db.Franchises.FirstOrDefaultAsync(x => x.FranchiseId == id);
        if (entity is null) return false;

        // Recommended:
        // Franchise là business entity có lịch sử vận hành, nên deactivate thay vì hard delete.
        if (string.Equals(entity.Status, StatusInactive, StringComparison.OrdinalIgnoreCase))
            return true;

        var old = ToAuditSnapshot(entity);

        entity.Status = StatusInactive;

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            FranchiseId = entity.FranchiseId,
            CentralKitchenId = entity.CentralKitchenId,
            Action = "DEACTIVATE",
            EntityName = nameof(Franchise),
            EntityId = entity.FranchiseId,
            OldDataJson = JsonSerializer.Serialize(old),
            NewDataJson = JsonSerializer.Serialize(ToAuditSnapshot(entity)),
            Reason = "Deactivate franchise",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return true;
    }

    private async Task EnsureCentralKitchenExistsAsync(int centralKitchenId)
    {
        if (centralKitchenId <= 0)
            throw new ArgumentException("CentralKitchenId must be greater than 0.", nameof(centralKitchenId));

        var exists = await _db.CentralKitchens
            .AsNoTracking()
            .AnyAsync(x => x.CentralKitchenId == centralKitchenId);

        if (!exists)
            throw new InvalidOperationException($"Central kitchen {centralKitchenId} does not exist.");
    }

    private static FranchiseResponseDto Map(Franchise x) => new()
    {
        FranchiseId = x.FranchiseId,
        CentralKitchenId = x.CentralKitchenId,
        CentralKitchenName = x.CentralKitchen?.Name,
        Name = x.Name,
        Type = x.Type,
        Status = x.Status,
        Address = x.Address,
        Location = x.Location,
        Latitude = x.Latitude,
        Longitude = x.Longitude,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };

    private static object ToAuditSnapshot(Franchise x) => new
    {
        x.FranchiseId,
        x.CentralKitchenId,
        x.Name,
        x.Type,
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
            throw new InvalidOperationException(
                $"Invalid franchise status '{status}'. Allowed values: {string.Join(", ", AllowedStatuses)}.");

        return normalized;
    }

    private void RequireAdminOrManager()
    {
        if (!_current.IsInRole(RoleNames.Admin) && !_current.IsInRole(RoleNames.Manager))
            throw new UnauthorizedAccessException("Only Admin/Manager can access franchises.");
    }

    private void RequireAdminOnly()
    {
        if (!_current.IsInRole(RoleNames.Admin))
            throw new UnauthorizedAccessException("Only Admin can perform this action.");
    }
}