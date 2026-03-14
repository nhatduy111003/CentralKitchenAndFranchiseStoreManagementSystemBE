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

        return await (
            from f in _db.Franchises.AsNoTracking()
            join ck in _db.CentralKitchens.AsNoTracking()
                on f.CentralKitchenId equals ck.CentralKitchenId into ckGroup
            from ck in ckGroup.DefaultIfEmpty()
            orderby f.CreatedAt descending
            select new FranchiseResponseDto
            {
                FranchiseId = f.FranchiseId,
                CentralKitchenId = f.CentralKitchenId,
                CentralKitchenName = ck != null ? ck.Name : null,
                Name = f.Name,
                Type = f.Type,
                Status = f.Status,
                Address = f.Address,
                Location = f.Location,
                Latitude = f.Latitude,
                Longitude = f.Longitude,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt
            }
        ).ToListAsync();
    }

    public async Task<FranchiseResponseDto?> GetByIdAsync(int id)
    {
        RequireAdminOrManager();

        return await (
            from f in _db.Franchises.AsNoTracking()
            join ck in _db.CentralKitchens.AsNoTracking()
                on f.CentralKitchenId equals ck.CentralKitchenId into ckGroup
            from ck in ckGroup.DefaultIfEmpty()
            where f.FranchiseId == id
            select new FranchiseResponseDto
            {
                FranchiseId = f.FranchiseId,
                CentralKitchenId = f.CentralKitchenId,
                CentralKitchenName = ck != null ? ck.Name : null,
                Name = f.Name,
                Type = f.Type,
                Status = f.Status,
                Address = f.Address,
                Location = f.Location,
                Latitude = f.Latitude,
                Longitude = f.Longitude,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt
            }
        ).FirstOrDefaultAsync();
    }

    public async Task<int> CreateAsync(FranchiseCreateDto dto)
    {
        RequireAdminOnly();
        ArgumentNullException.ThrowIfNull(dto);

        await EnsureCentralKitchenExistsAsync(dto.CentralKitchenId);

        var now = DateTime.UtcNow;

        var entity = new Franchise
        {
            CentralKitchenId = dto.CentralKitchenId,
            Name = NormalizeRequired(dto.Name, nameof(dto.Name)),
            Type = NormalizeRequired(dto.Type, nameof(dto.Type)),
            Status = NormalizeStatus(dto.Status, StatusActive),
            Address = NormalizeNullable(dto.Address),
            Location = NormalizeNullable(dto.Location),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            CentralKitchenId = dto.CentralKitchenId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
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

        await EnsureCentralKitchenExistsAsync(dto.CentralKitchenId);

        var entity = await _db.Franchises.FirstOrDefaultAsync(x => x.FranchiseId == id);
        if (entity is null) return false;

        var old = ToAuditSnapshot(entity);

        entity.CentralKitchenId = dto.CentralKitchenId;
        entity.Name = NormalizeRequired(dto.Name, nameof(dto.Name));
        entity.Type = NormalizeRequired(dto.Type, nameof(dto.Type));
        entity.Status = NormalizeStatus(dto.Status, entity.Status);
        entity.Address = NormalizeNullable(dto.Address);
        entity.Location = NormalizeNullable(dto.Location);
        entity.Latitude = dto.Latitude;
        entity.Longitude = dto.Longitude;
        entity.UpdatedAt = DateTime.UtcNow;

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
        var hasUsers = await _db.UserWorkAssignments
    .AnyAsync(x => x.FranchiseId == id);

        var entity = await _db.Franchises.FirstOrDefaultAsync(x => x.FranchiseId == id);
        if (entity is null) return false;

        // Recommended:
        // Franchise là business entity có lịch sử vận hành, nên deactivate thay vì hard delete.
        if (string.Equals(entity.Status, StatusInactive, StringComparison.OrdinalIgnoreCase))
            return true;

        var old = ToAuditSnapshot(entity);

        entity.Status = StatusInactive;
        entity.UpdatedAt = DateTime.UtcNow;

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