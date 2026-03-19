using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.StoreOrders;
using CentralKitchenAndFranchise.DTO.Responses.StoreOrders;
using Microsoft.EntityFrameworkCore;
using PayOS.Exceptions;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class KitchenOrderService : IKitchenOrderService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFranchiseAccessService _access;
    public KitchenOrderService(AppDbContext db, ICurrentUserService current, IFranchiseAccessService access)
    {
        _db = db;
        _current = current;
        _access = access;
    }

    public async Task<IncomingOrderDetailResponse> GetDetailAsync(int centralKitchenId, int orderId, CancellationToken ct = default)
    {
        await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

        var order = await _db.StoreOrders
            .AsNoTracking()
            .Include(x => x.Franchise)
            .Include(x => x.Items)
                .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x =>
                x.StoreOrderId == orderId &&
                x.Franchise.CentralKitchenId == centralKitchenId, ct);

        if (order is null)
            throw new NotFoundException("Store order not found.");

        var receivedBy = await ResolveUsernameAsync(order.ReceivedByUserId, ct);
        var forwardedBy = await ResolveUsernameAsync(order.ForwardedByUserId, ct);

        return new IncomingOrderDetailResponse
        {
            StoreOrderId = order.StoreOrderId,
            OrderCode = BuildOrderCode(order.StoreOrderId),
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            RequestedDeliveryDate = order.OrderDate,
            StoreNote = order.StoreNote ?? order.StoreNote,
            StoreId = order.FranchiseId,
            StoreName = order.Franchise.Name,
            StoreAddress = order.Franchise.Address,
            TotalItems = order.Items.Count,
            TotalQuantity = order.Items.Sum(i => i.Quantity),
            ReceivedAt = order.ReceivedAt,
            ReceivedBy = receivedBy,
            ForwardedAt = order.ForwardedAt,
            ForwardedBy = forwardedBy,
            ProcessingNote = order.ProcessingNote,
            Items = order.Items
                .OrderBy(i => i.ProductId)
                .Select(i => new IncomingOrderDetailItemResponse
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    Unit = i.Product.Unit,
                    Quantity = i.Quantity
                })
                .ToList()
        };
    }

    public async Task<OrderWorkflowActionResponse> ReceiveAsync(
        int centralKitchenId,
        int orderId,
        ReceiveIncomingOrderRequest request,
        CancellationToken ct = default)
    {
        await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

        var order = await LoadManagedOrderAsync(centralKitchenId, orderId, ct);

        if (!string.Equals(order.Status, StoreOrderStatus.Locked, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Only LOCKED orders can be received by kitchen.");

        var now = DateTime.UtcNow;
        var currentUserId = _current.UserId;

        var oldStatus = order.Status;

        order.Status = StoreOrderStatus.ReceivedByKitchen;
        order.ReceivedAt = now;
        order.ReceivedByUserId = currentUserId;
        order.ReceiveNote = request.ReceiveNote;

        await AddHistoryAsync(
            order.StoreOrderId,
            StoreOrderHistoryActions.OrderReceivedByKitchen,
            "Kitchen đã tiếp nhận đơn",
            oldStatus,
            order.Status,
            request.ReceiveNote,
            currentUserId,
            now,
            ct);

        await AddAuditLogAsync("STORE_ORDER_RECEIVED_BY_KITCHEN", order.StoreOrderId, oldStatus, order.Status, request.ReceiveNote, now, ct);

        await _db.SaveChangesAsync(ct);

        return new OrderWorkflowActionResponse
        {
            StoreOrderId = order.StoreOrderId,
            Status = order.Status,
            ReceivedAt = order.ReceivedAt,
            ReceivedBy = await ResolveUsernameAsync(order.ReceivedByUserId, ct),
            Message = "Kitchen received the order successfully."
        };
    }

    public async Task<OrderWorkflowActionResponse> UpdateProcessingNoteAsync(
        int centralKitchenId,
        int orderId,
        UpdateProcessingNoteRequest request,
        CancellationToken ct = default)
    {
        await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

        var order = await LoadManagedOrderAsync(centralKitchenId, orderId, ct);

        var allowed = new[]
        {
            StoreOrderStatus.Locked,
            StoreOrderStatus.ReceivedByKitchen,
            StoreOrderStatus.ForwardedToSupply
        };

        if (!allowed.Contains(order.Status, StringComparer.OrdinalIgnoreCase))
            throw new BadRequestException("Processing note can only be updated on active kitchen/supply workflow orders.");

        var now = DateTime.UtcNow;
        var currentUserId = _current.UserId;

        order.ProcessingNote = request.ProcessingNote.Trim();
        order.ProcessingNoteUpdatedAt = now;
        order.ProcessingNoteUpdatedByUserId = currentUserId;

        await AddHistoryAsync(
            order.StoreOrderId,
            StoreOrderHistoryActions.ProcessingNoteUpdated,
            "Cập nhật ghi chú xử lý",
            order.Status,
            order.Status,
            order.ProcessingNote,
            currentUserId,
            now,
            ct);

        await AddAuditLogAsync("STORE_ORDER_PROCESSING_NOTE_UPDATED", order.StoreOrderId, order.Status, order.Status, order.ProcessingNote, now, ct);

        await _db.SaveChangesAsync(ct);

        return new OrderWorkflowActionResponse
        {
            StoreOrderId = order.StoreOrderId,
            Status = order.Status,
            ProcessingNote = order.ProcessingNote,
            UpdatedAt = order.ProcessingNoteUpdatedAt,
            UpdatedBy = await ResolveUsernameAsync(order.ProcessingNoteUpdatedByUserId, ct),
            Message = "Processing note updated successfully."
        };
    }

    public async Task<OrderWorkflowActionResponse> ForwardToSupplyAsync(
        int centralKitchenId,
        int orderId,
        ForwardToSupplyRequest request,
        CancellationToken ct = default)
    {
        await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

        var order = await LoadManagedOrderAsync(centralKitchenId, orderId, ct);

        if (!string.Equals(order.Status, StoreOrderStatus.ReceivedByKitchen, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Only orders received by kitchen can be forwarded to supply.");

        var now = DateTime.UtcNow;
        var currentUserId = _current.UserId;

        var oldStatus = order.Status;

        order.Status = StoreOrderStatus.ForwardedToSupply;
        order.ForwardedAt = now;
        order.ForwardedByUserId = currentUserId;
        order.ForwardNote = request.ForwardNote;

        await AddHistoryAsync(
            order.StoreOrderId,
            StoreOrderHistoryActions.OrderForwardedToSupply,
            "Kitchen đã chuyển đơn sang Supply",
            oldStatus,
            order.Status,
            request.ForwardNote,
            currentUserId,
            now,
            ct);

        await AddAuditLogAsync("STORE_ORDER_FORWARDED_TO_SUPPLY", order.StoreOrderId, oldStatus, order.Status, request.ForwardNote, now, ct);

        await _db.SaveChangesAsync(ct);

        return new OrderWorkflowActionResponse
        {
            StoreOrderId = order.StoreOrderId,
            Status = order.Status,
            ForwardedAt = order.ForwardedAt,
            ForwardedBy = await ResolveUsernameAsync(order.ForwardedByUserId, ct),
            ForwardNote = order.ForwardNote,
            Message = "Order forwarded to supply successfully."
        };
    }

    public async Task<List<StoreOrderHistoryResponse>> GetHistoryAsync(int centralKitchenId, int orderId, CancellationToken ct = default)
    {
        await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

        var exists = await _db.StoreOrders
            .AsNoTracking()
            .AnyAsync(x => x.StoreOrderId == orderId && x.Franchise.CentralKitchenId == centralKitchenId, ct);

        if (!exists)
            throw new NotFoundException("Store order not found.");

        var histories = await _db.Set<StoreOrderHistory>()
            .AsNoTracking()
            .Where(x => x.StoreOrderId == orderId)
            .OrderByDescending(x => x.PerformedAt)
            .ToListAsync(ct);

        var userIds = histories
            .Where(x => x.PerformedByUserId.HasValue)
            .Select(x => x.PerformedByUserId!.Value)
            .Distinct()
            .ToList();

        var users = await _db.Users
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.Username, ct);

        return histories.Select(x => new StoreOrderHistoryResponse
        {
            HistoryId = x.StoreOrderHistoryId,
            StoreOrderId = x.StoreOrderId,
            ActionType = x.ActionType,
            ActionLabel = x.ActionLabel,
            OldStatus = x.OldStatus,
            NewStatus = x.NewStatus,
            Note = x.Note,
            PerformedAt = x.PerformedAt,
            PerformedBy = x.PerformedByUserId.HasValue && users.TryGetValue(x.PerformedByUserId.Value, out var username)
                ? username
                : null
        }).ToList();
    }

    private async Task<StoreOrder> LoadManagedOrderAsync(int centralKitchenId, int orderId, CancellationToken ct)
    {
        var order = await _db.StoreOrders
            .Include(x => x.Franchise)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x =>
                x.StoreOrderId == orderId &&
                x.Franchise.CentralKitchenId == centralKitchenId, ct);

        if (order is null)
            throw new NotFoundException("Store order not found.");

        return order;
    }

    private async Task AddHistoryAsync(
        int storeOrderId,
        string actionType,
        string actionLabel,
        string? oldStatus,
        string? newStatus,
        string? note,
        int? performedByUserId,
        DateTime performedAt,
        CancellationToken ct)
    {
        _db.Set<StoreOrderHistory>().Add(new StoreOrderHistory
        {
            StoreOrderId = storeOrderId,
            ActionType = actionType,
            ActionLabel = actionLabel,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Note = note,
            PerformedByUserId = performedByUserId,
            PerformedAt = performedAt
        });

        await Task.CompletedTask;
    }

    private async Task AddAuditLogAsync(
        string action,
        int entityId,
        string? oldStatus,
        string? newStatus,
        string? note,
        DateTime now,
        CancellationToken ct)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = _current.UserId,
            Action = action,
            EntityName = "StoreOrder",
            EntityId = entityId,
            OldDataJson = oldStatus is null ? null : $"{{\"status\":\"{oldStatus}\"}}",
            NewDataJson = $"{{\"status\":\"{newStatus}\",\"note\":{(note is null ? "null" : $"\"{note}\"")}}}",
            CreatedAt = now
        });

        await Task.CompletedTask;
    }

    private async Task<string?> ResolveUsernameAsync(int? userId, CancellationToken ct)
    {
        if (!userId.HasValue)
            return null;

        var user = await _db.Users
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .Select(x => x.Username)
            .FirstOrDefaultAsync(ct);

        return user;
    }

    private static string BuildOrderCode(int storeOrderId)
        => $"SO-{storeOrderId:D6}";
}