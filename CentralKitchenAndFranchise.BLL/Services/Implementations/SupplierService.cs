using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.UnitOfWork;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Suppliers;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class SupplierService : ISupplierService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public SupplierService(IUnitOfWork uow, AppDbContext db, ICurrentUserService current)
    {
        _uow = uow;
        _db = db;
        _current = current;
    }

    public async Task<PagedResult<SupplierResponse>> SearchAsync(SupplierListQuery query, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        query ??= new SupplierListQuery();

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        if (pageSize > 200) pageSize = 200;

        var status = (query.Status ?? SupplierStatus.Active).Trim().ToUpperInvariant();
        if (status is not (SupplierStatus.Active or SupplierStatus.Inactive or "ALL"))
            throw new ArgumentException("status must be ACTIVE, INACTIVE, or ALL.");

        var sortBy = (query.SortBy ?? "name").Trim().ToLowerInvariant();
        var sortDir = (query.SortDir ?? "asc").Trim().ToLowerInvariant();
        if (sortDir is not ("asc" or "desc"))
            throw new ArgumentException("sortDir must be asc or desc.");

        IQueryable<Supplier> q = _db.Suppliers.AsNoTracking();

        // default soft-delete policy: only ACTIVE unless status=ALL
        if (status != "ALL")
            q = q.Where(x => x.Status == status);

        // search by name (and optional contactInfo)
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            q = q.Where(x =>
                EF.Functions.ILike(x.Name, $"%{term}%") ||
                (x.ContactInfo != null && EF.Functions.ILike(x.ContactInfo, $"%{term}%"))
            );
        }

        var total = await q.CountAsync(ct);

        q = ApplySort(q, sortBy, sortDir);

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<SupplierResponse>.Create(
            items: items.Select(ToDto).ToList(),
            page: page,
            pageSize: pageSize,
            totalItems: total
        );
    }

    public async Task<SupplierResponse> GetByIdAsync(int id, CancellationToken ct = default)
    {
        RequireAdminOrManager();
        var entity = await _uow.Suppliers.GetByIdAsync(id, ct);
        if (entity is null) throw new KeyNotFoundException($"Supplier {id} not found.");
        return ToDto(entity);
    }

    public async Task<SupplierResponse> CreateAsync(CreateSupplierRequest request, CancellationToken ct = default)
    {
        RequireAdminOrManager();

        if (request is null) throw new ArgumentException("Request body is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.");

        var entity = new Supplier
        {
            Name = request.Name.Trim(),
            ContactInfo = string.IsNullOrWhiteSpace(request.ContactInfo) ? null : request.ContactInfo.Trim(),
            Status = SupplierStatus.Active
        };

        await _uow.Suppliers.AddAsync(entity, ct);

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // rely on unique index (if exists) OR just generic conflict
            throw new InvalidOperationException("Supplier name already exists.");
        }

        await AddAuditAsync(
            action: "SUPPLIER_CREATE",
            entityName: "Supplier",
            entityId: entity.SupplierId,
            oldObj: null,
            newObj: entity,
            reason: null,
            ct: ct);

        return ToDto(entity);
    }

    public async Task<SupplierResponse> UpdateAsync(int id, UpdateSupplierRequest request, CancellationToken ct = default)
    {
        RequireAdminOrManager();

        if (request is null) throw new ArgumentException("Request body is required.");

        var entity = await _uow.Suppliers.GetByIdAsync(id, ct);
        if (entity is null) throw new KeyNotFoundException($"Supplier {id} not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.");

        var old = new { entity.Name, entity.ContactInfo, entity.Status };

        entity.Name = request.Name.Trim();
        entity.ContactInfo = string.IsNullOrWhiteSpace(request.ContactInfo) ? null : request.ContactInfo.Trim();

        _uow.Suppliers.Update(entity);

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("Supplier name already exists.");
        }

        await AddAuditAsync(
            action: "SUPPLIER_UPDATE",
            entityName: "Supplier",
            entityId: entity.SupplierId,
            oldObj: old,
            newObj: entity,
            reason: null,
            ct: ct);

        return ToDto(entity);
    }

    public async Task<SupplierResponse> ChangeStatusAsync(int id, ChangeSupplierStatusRequest request, CancellationToken ct = default)
    {
        RequireAdminOrManager();

        if (request is null) throw new ArgumentException("Request body is required.");

        var entity = await _uow.Suppliers.GetByIdAsync(id, ct);
        if (entity is null) throw new KeyNotFoundException($"Supplier {id} not found.");

        if (string.IsNullOrWhiteSpace(request.Status))
            throw new ArgumentException("Status is required.");

        var newStatus = request.Status.Trim().ToUpperInvariant();
        if (newStatus is not (SupplierStatus.Active or SupplierStatus.Inactive))
            throw new ArgumentException("Status must be ACTIVE or INACTIVE.");

        var old = new { entity.Status };

        entity.Status = newStatus;

        _uow.Suppliers.Update(entity);
        await _uow.SaveChangesAsync(ct);

        await AddAuditAsync(
            action: "SUPPLIER_CHANGE_STATUS",
            entityName: "Supplier",
            entityId: entity.SupplierId,
            oldObj: old,
            newObj: new { entity.Status },
            reason: string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            ct: ct);

        return ToDto(entity);
    }

    public Task DeleteAsync(int id, CancellationToken ct = default)
        => ChangeStatusAsync(id, new ChangeSupplierStatusRequest
        {
            Status = SupplierStatus.Inactive,
            Reason = "Deactivated via DELETE endpoint"
        }, ct);

    private void RequireAdminOrManager()
    {
        var role = _current.Role;
        if (role != RoleNames.Admin && role != RoleNames.Manager)
            throw new ForbiddenAccessException("Only Admin/Manager can perform this action.");
    }

    private async Task AddAuditAsync(string action, string entityName, int entityId, object? oldObj, object? newObj, string? reason, CancellationToken ct)
    {
        var log = new AuditLog
        {
            UserId = _current.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldDataJson = oldObj is null ? null : JsonSerializer.Serialize(oldObj),
            NewDataJson = newObj is null ? null : JsonSerializer.Serialize(newObj),
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }

    private static IQueryable<Supplier> ApplySort(IQueryable<Supplier> q, string sortBy, string sortDir)
    {
        var desc = sortDir == "desc";

        return sortBy switch
        {
            "id" => desc ? q.OrderByDescending(x => x.SupplierId) : q.OrderBy(x => x.SupplierId),

            "status" => desc
                ? q.OrderByDescending(x => x.Status).ThenByDescending(x => x.SupplierId)
                : q.OrderBy(x => x.Status).ThenBy(x => x.SupplierId),

            "name" or _ => desc
                ? q.OrderByDescending(x => x.Name).ThenByDescending(x => x.SupplierId)
                : q.OrderBy(x => x.Name).ThenBy(x => x.SupplierId),
        };
    }

    private static SupplierResponse ToDto(Supplier x) => new()
    {
        Id = x.SupplierId,
        Name = x.Name,
        ContactInfo = x.ContactInfo,
        Status = x.Status
    };
}
