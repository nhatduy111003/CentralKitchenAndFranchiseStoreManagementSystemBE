using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Responses;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class FranchiseService : IFranchiseService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public FranchiseService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<FranchiseDto>> GetAllAsync()
    {
        RequireAdminOrManager();

        var items = await _db.Franchises
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return items.Select(Map).ToList();
    }

    public async Task<FranchiseDto?> GetByIdAsync(int id)
    {
        RequireAdminOrManager();

        var entity = await _db.Franchises.AsNoTracking().FirstOrDefaultAsync(x => x.FranchiseId == id);
        return entity is null ? null : Map(entity);
    }

    public async Task<int> CreateAsync(FranchiseCreateDto dto)
    {
        RequireAdminOnly();
        dto = dto ?? throw new ArgumentNullException(nameof(dto));

        var entity = new Franchise
        {
            Name = dto.Name.Trim(),
            Type = dto.Type.Trim(),
            Status = (dto.Status ?? "ACTIVE").Trim().ToUpperInvariant(),
            Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim(),
            Location = string.IsNullOrWhiteSpace(dto.Location) ? null : dto.Location.Trim(),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            CentralKitchenId = dto.CentralKitchenId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.Franchises.AddAsync(entity);

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            FranchiseId = null,
            Action = "CREATE",
            EntityName = nameof(Franchise),
            EntityId = null,
            NewDataJson = JsonSerializer.Serialize(new { entity.Name, entity.Type, entity.Status }),
            Reason = "Create franchise",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return entity.FranchiseId;
    }

    public async Task<bool> UpdateAsync(int id, FranchiseCreateDto dto)
    {
        RequireAdminOnly();
        dto = dto ?? throw new ArgumentNullException(nameof(dto));

        var entity = await _db.Franchises.FirstOrDefaultAsync(x => x.FranchiseId == id);
        if (entity is null) return false;

        var old = new
        {
            entity.Name,
            entity.Type,
            entity.Status,
            entity.Address,
            entity.Location,
            entity.Latitude,
            entity.Longitude
        };

        entity.Name = dto.Name.Trim();
        entity.Type = dto.Type.Trim();
        entity.Status = (dto.Status ?? entity.Status).Trim().ToUpperInvariant();
        entity.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
        entity.Location = string.IsNullOrWhiteSpace(dto.Location) ? null : dto.Location.Trim();
        entity.Latitude = dto.Latitude;
        entity.Longitude = dto.Longitude;

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            FranchiseId = entity.FranchiseId,
            Action = "UPDATE",
            EntityName = nameof(Franchise),
            EntityId = entity.FranchiseId,
            OldDataJson = JsonSerializer.Serialize(old),
            NewDataJson = JsonSerializer.Serialize(new { entity.Name, entity.Type, entity.Status, entity.Address, entity.Location, entity.Latitude, entity.Longitude }),
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

        if (hasUsers)
            throw new InvalidOperationException("Không thể xóa người dùng đã gán !");
        var entity = await _db.Franchises.FirstOrDefaultAsync(x => x.FranchiseId == id);
        if (entity is null) return false;

        var old = new
        {
            entity.FranchiseId,
            entity.Name,
            entity.Type,
            entity.Status
        };

        _db.Franchises.Remove(entity);

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            FranchiseId = entity.FranchiseId,
            Action = "DELETE",
            EntityName = nameof(Franchise),
            EntityId = entity.FranchiseId,
            OldDataJson = JsonSerializer.Serialize(old),
            NewDataJson = null,
            Reason = "Hard delete franchise",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return true;
    }

    private static FranchiseDto Map(Franchise x) => new()
    {
        FranchiseId = x.FranchiseId,
        Name = x.Name,
        Type = x.Type,
        Status = x.Status,
        Address = x.Address,
        Location = x.Location,
        Latitude = x.Latitude,
        Longitude = x.Longitude
    };

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