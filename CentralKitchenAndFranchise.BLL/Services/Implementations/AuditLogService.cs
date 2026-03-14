using System.Text;
using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.AuditLogs;
using CentralKitchenAndFranchise.DTO.Responses.AuditLogs;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public AuditLogService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PagedResult<AuditLogResponse>> SearchAsync(AuditLogListQuery query, CancellationToken ct = default)
    {
        RequireAdminOnly();

        query ??= new AuditLogListQuery();
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        if (pageSize > 500) pageSize = 500;

        var sortBy = (query.SortBy ?? "createdAt").Trim().ToLowerInvariant();
        var sortDir = (query.SortDir ?? "desc").Trim().ToLowerInvariant();
        if (sortDir is not ("asc" or "desc"))
            throw new ArgumentException("sortDir must be asc or desc.");

        IQueryable<AuditLog> q = _db.AuditLogs
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Franchise);

        if (query.UserId.HasValue)
            q = q.Where(x => x.UserId == query.UserId.Value);

        if (query.FranchiseId.HasValue)
            q = q.Where(x => x.FranchiseId == query.FranchiseId.Value);

        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            var en = query.EntityName.Trim();
            q = q.Where(x => x.EntityName == en);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var act = query.Action.Trim().ToUpperInvariant();
            q = q.Where(x => x.Action == act);
        }

        if (query.FromUtc.HasValue)
            q = q.Where(x => x.CreatedAt >= DateTime.SpecifyKind(query.FromUtc.Value, DateTimeKind.Utc));

        if (query.ToUtc.HasValue)
            q = q.Where(x => x.CreatedAt <= DateTime.SpecifyKind(query.ToUtc.Value, DateTimeKind.Utc));

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            q = q.Where(x =>
                EF.Functions.ILike(x.Action, $"%{term}%") ||
                (x.EntityName != null && EF.Functions.ILike(x.EntityName, $"%{term}%")) ||
                (x.Reason != null && EF.Functions.ILike(x.Reason, $"%{term}%")));
        }

        var total = await q.CountAsync(ct);

        q = ApplySort(q, sortBy, sortDir);

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<AuditLogResponse>.Create(
            items: items.Select(Map).ToList(),
            totalItems: total,
            page: page,
            pageSize: pageSize);
    }

    public async Task<byte[]> ExportCsvAsync(AuditLogListQuery query, CancellationToken ct = default)
    {
        RequireAdminOnly();

        // export all matching rows (cap to avoid OOM)
        query ??= new AuditLogListQuery();
        query.Page = 1;
        query.PageSize = 5000;

        var paged = await SearchAsync(query, ct);
        var sb = new StringBuilder();

        sb.AppendLine("Id,CreatedAtUtc,Action,EntityName,EntityId,UserId,UserName,FranchiseId,FranchiseName,Reason");

        foreach (var x in paged.Items)
        {
            sb.Append(Escape(x.Id.ToString())).Append(',')
              .Append(Escape(x.CreatedAt.ToString("O"))).Append(',')
              .Append(Escape(x.Action)).Append(',')
              .Append(Escape(x.EntityName)).Append(',')
              .Append(Escape(x.EntityId?.ToString())).Append(',')
              .Append(Escape(x.UserId?.ToString())).Append(',')
              .Append(Escape(x.UserName)).Append(',')
              .Append(Escape(x.FranchiseId?.ToString())).Append(',')
              .Append(Escape(x.FranchiseName)).Append(',')
              .Append(Escape(x.Reason))
              .AppendLine();
        }

        var utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return utf8Bom.Concat(bytes).ToArray();
    }

    private static string Escape(string? s)
    {
        s ??= "";
        if (s.Contains('\"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return $"\"{s.Replace("\"", "\"\"")}\"";
        return s;
    }

    private static IQueryable<AuditLog> ApplySort(IQueryable<AuditLog> q, string sortBy, string sortDir)
    {
        var desc = sortDir == "desc";
        return sortBy switch
        {
            "createdat" => desc ? q.OrderByDescending(x => x.CreatedAt) : q.OrderBy(x => x.CreatedAt),
            "action" => desc ? q.OrderByDescending(x => x.Action) : q.OrderBy(x => x.Action),
            "entityname" => desc ? q.OrderByDescending(x => x.EntityName) : q.OrderBy(x => x.EntityName),
            _ => throw new ArgumentException("sortBy must be createdAt, action, or entityName.")
        };
    }

    private static AuditLogResponse Map(AuditLog x) => new()
    {
        Id = x.AuditLogId,
        UserId = x.UserId,
        UserName = x.User?.Username,
        FranchiseId = x.FranchiseId,
        FranchiseName = x.Franchise?.Name,
        Action = x.Action,
        EntityName = x.EntityName,
        EntityId = x.EntityId,
        Reason = x.Reason,
        CreatedAt = x.CreatedAt
    };

    private void RequireAdminOnly()
    {
        if (!_current.IsInRole(RoleNames.Admin))
            throw new ForbiddenAccessException("Only Admin can view/export audit logs.");
    }
}