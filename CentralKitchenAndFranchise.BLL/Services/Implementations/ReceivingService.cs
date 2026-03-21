using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.Receivings;
using CentralKitchenAndFranchise.DTO.Responses.Receivings;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class ReceivingService : IReceivingService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFranchiseAccessService _access;
    private readonly IInventoryTransferService _transferService;

    public ReceivingService(
        AppDbContext db,
        ICurrentUserService current,
        IFranchiseAccessService access,
        IInventoryTransferService transferService)
    {
        _db = db;
        _current = current;
        _access = access;
        _transferService = transferService;
    }

    public async Task<List<ReceivingListItemResponse>> GetPendingAsync(
        int franchiseId,
        CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff);
        await _access.EnsureCanAccessAsync(franchiseId, ct);

        // Pending receiving = delivery đã được Supply mark DELIVERED
        // nhưng Store chưa confirm => chưa có ReceivingReport
        var deliveries = await _db.Deliveries
            .AsNoTracking()
            .Include(d => d.DeliveryPlan)
            .Include(d => d.FromCentralKitchen)
            .Include(d => d.ReceivingReports)
            .Include(d => d.ProductItems)
            .Include(d => d.IngredientItems)
            .Where(d =>
                d.DeliveryPlan.FranchiseId == franchiseId &&
                d.Status == DeliveryStatus.Delivered &&
                !d.ReceivingReports.Any())
            .OrderByDescending(d => d.DeliveredAt ?? d.CreatedAt)
            .ThenByDescending(d => d.DeliveryId)
            .ToListAsync(ct);

        return deliveries.Select(d => new ReceivingListItemResponse
        {
            ReceivingId = d.DeliveryId,
            DeliveryCode = BuildDeliveryCode(d.DeliveryId),

            FranchiseId = d.DeliveryPlan.FranchiseId,

            // dùng FromCentralKitchenId vì luôn có trên Delivery
            CentralKitchenId = d.FromCentralKitchenId,
            CentralKitchenName = d.FromCentralKitchen?.Name ?? "(unknown)",

            PlanDate = d.DeliveryPlan.PlannedDate,
            DeliveryDate = d.DeliveredAt ?? d.CreatedAt,
            CreatedAt = d.CreatedAt,

            // đây là pending list nên status trả "PENDING" cho FE dùng trực tiếp
            Status = "PENDING",

            TotalItems = d.ProductItems.Count + d.IngredientItems.Count,
            TotalQuantity = d.ProductItems.Sum(x => x.Quantity) + d.IngredientItems.Sum(x => x.Quantity),

            // FIX: branch hiện tại đã có link StoreOrderId
            StoreOrderId = d.DeliveryPlan.StoreOrderId,
            OrderCode = d.DeliveryPlan.StoreOrderId.HasValue
                ? BuildOrderCode(d.DeliveryPlan.StoreOrderId.Value)
                : null
        }).ToList();
    }

    public async Task<ReceivingDetailResponse> GetByIdAsync(
        int franchiseId,
        int deliveryId,
        CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff);
        await _access.EnsureCanAccessAsync(franchiseId, ct);

        var delivery = await _db.Deliveries
            .AsNoTracking()
            .Include(d => d.DeliveryPlan)
                .ThenInclude(p => p.Franchise)
            .Include(d => d.FromCentralKitchen)
            .Include(d => d.ProductItems)
                .ThenInclude(x => x.Product)
            .Include(d => d.IngredientItems)
                .ThenInclude(x => x.Ingredient)
            .Include(d => d.ReceivingReports)
            .FirstOrDefaultAsync(d =>
                d.DeliveryId == deliveryId &&
                d.DeliveryPlan.FranchiseId == franchiseId,
                ct);

        if (delivery is null)
            throw new KeyNotFoundException($"Receiving/Delivery {deliveryId} not found.");

        var receivingStatus = ResolveReceivingStatus(delivery);
        var latestReport = delivery.ReceivingReports
            .OrderByDescending(x => x.ReceivedAt)
            .FirstOrDefault();

        var isConfirmed = receivingStatus == "RECEIVED";

        var response = new ReceivingDetailResponse
        {
            ReceivingId = delivery.DeliveryId,
            DeliveryCode = BuildDeliveryCode(delivery.DeliveryId),
            Status = receivingStatus,

            CentralKitchenId = delivery.FromCentralKitchenId,
            CentralKitchenName = delivery.FromCentralKitchen?.Name ?? "(unknown)",

            FranchiseId = delivery.DeliveryPlan.FranchiseId,
            FranchiseName = delivery.DeliveryPlan.Franchise.Name,

            PlanDate = delivery.DeliveryPlan.PlannedDate,
            DeliveryDate = delivery.DeliveredAt ?? delivery.CreatedAt,
            CreatedAt = delivery.CreatedAt,

            // FIX: lấy note từ receiving report nếu đã confirm
            Note = latestReport?.Note,

            // FIX: đã có link StoreOrderId trên DeliveryPlan
            StoreOrderId = delivery.DeliveryPlan.StoreOrderId,
            OrderCode = delivery.DeliveryPlan.StoreOrderId.HasValue
                ? BuildOrderCode(delivery.DeliveryPlan.StoreOrderId.Value)
                : null,

            Items = new List<ReceivingDetailLineResponse>()
        };

        response.Items.AddRange(delivery.ProductItems.Select(x => new ReceivingDetailLineResponse
        {
            ItemType = "PRODUCT",
            ItemId = x.ProductId,
            ItemName = x.Product?.Name ?? "(unknown)",
            Unit = x.Product?.Unit ?? "",
            ExpectedQuantity = x.Quantity,
            DeliveredQuantity = x.Quantity,

            // Phase 1 chưa có partial receive line-level
            // Nếu đã confirm thì assume nhận full
            ReceivedQuantity = isConfirmed ? x.Quantity : null
        }));

        response.Items.AddRange(delivery.IngredientItems.Select(x => new ReceivingDetailLineResponse
        {
            ItemType = "INGREDIENT",
            ItemId = x.IngredientId,
            ItemName = x.Ingredient?.Name ?? "(unknown)",
            Unit = x.Ingredient?.Unit ?? "",
            ExpectedQuantity = x.Quantity,
            DeliveredQuantity = x.Quantity,
            ReceivedQuantity = isConfirmed ? x.Quantity : null
        }));

        return response;
    }

    public async Task<ReceivingConfirmResponse> ConfirmAsync(
        int franchiseId,
        int deliveryId,
        ConfirmReceivingRequest request,
        CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.StoreStaff);
        await _access.EnsureCanAccessAsync(franchiseId, ct);

        var delivery = await _db.Deliveries
            .Include(d => d.DeliveryPlan)
            .Include(d => d.ReceivingReports)
            .FirstOrDefaultAsync(d =>
                d.DeliveryId == deliveryId &&
                d.DeliveryPlan.FranchiseId == franchiseId,
                ct);

        if (delivery is null)
            throw new KeyNotFoundException($"Receiving/Delivery {deliveryId} not found.");

        // Phase 1 đúng nghĩa:
        // Supply mark DELIVERED -> Store confirm -> delivery thành CONFIRMED
        if (delivery.Status != DeliveryStatus.Delivered)
            throw new InvalidOperationException("Only DELIVERED deliveries can be confirmed by store.");

        if (delivery.ReceivingReports.Any())
            throw new InvalidOperationException("This receiving has already been confirmed.");

        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Transfer tồn kho thật sự chỉ xảy ra ở đây.
        //await _transferService.TransferDeliveryAsync(
        //    delivery.DeliveryId,
        //    delivery.FromCentralKitchenId,
        //    franchiseId,
        //    now,
        //    ct);

        var report = new ReceivingReport
        {
            DeliveryId = delivery.DeliveryId,
            ReceivedAt = now,

            // FIX: lưu tracking đầy đủ
            ReceivedByUserId = _current.UserId,
            Note = string.IsNullOrWhiteSpace(request?.Note) ? null : request.Note.Trim()
        };

        _db.ReceivingReports.Add(report);

        delivery.Status = DeliveryStatus.Confirmed;
        delivery.ConfirmedAt = now;

        // FIX: nếu delivery plan có link order thì chốt order sang RECEIVED_BY_STORE
        if (delivery.DeliveryPlan.StoreOrderId.HasValue)
        {
            var order = await _db.StoreOrders
                .FirstOrDefaultAsync(x => x.StoreOrderId == delivery.DeliveryPlan.StoreOrderId.Value, ct);

            if (order is not null)
            {
                var oldStatus = order.Status;
                order.Status = StoreOrderStatus.ReceivedByStore;

                _db.Set<StoreOrderHistory>().Add(new StoreOrderHistory
                {
                    StoreOrderId = order.StoreOrderId,
                    ActionType = StoreOrderHistoryActions.OrderReceivedByStore,
                    ActionLabel = "Store đã xác nhận nhận hàng",
                    OldStatus = oldStatus,
                    NewStatus = order.Status,
                    Note = report.Note,
                    PerformedByUserId = _current.UserId,
                    PerformedAt = now
                });
            }
        }

        // Save trước để có ReceivingReportId thật
        await _db.SaveChangesAsync(ct);

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = _current.UserId,
            FranchiseId = franchiseId,
            CentralKitchenId = delivery.FromCentralKitchenId,
            Action = "RECEIVING_CONFIRM",
            EntityName = "Delivery",
            EntityId = delivery.DeliveryId,
            OldDataJson = JsonSerializer.Serialize(new
            {
                DeliveryStatus = DeliveryStatus.Delivered
            }),
            NewDataJson = JsonSerializer.Serialize(new
            {
                DeliveryStatus = delivery.Status,
                delivery.ConfirmedAt,
                ReceivingReportId = report.ReceivingReportId,
                report.ReceivedAt,
                report.ReceivedByUserId,
                report.Note
            }),
            Reason = report.Note,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new ReceivingConfirmResponse
        {
            ReceivingId = delivery.DeliveryId,
            DeliveryCode = BuildDeliveryCode(delivery.DeliveryId),
            Status = "RECEIVED",
            ConfirmedAt = report.ReceivedAt,
            InventoryUpdated = true
        };
    }

    private static string ResolveReceivingStatus(Delivery delivery)
    {
        // ReceivingReport là source of truth mạnh nhất
        if (delivery.ReceivingReports.Any())
            return "RECEIVED";

        if (string.Equals(delivery.Status, DeliveryStatus.Confirmed, StringComparison.OrdinalIgnoreCase))
            return "RECEIVED";

        if (string.Equals(delivery.Status, DeliveryStatus.Delivered, StringComparison.OrdinalIgnoreCase))
            return "PENDING";

        return delivery.Status;
    }

    private void RequireOneOf(params string[] roles)
    {
        var currentRole = _current.Role;

        if (roles.Any(r => string.Equals(r, currentRole, StringComparison.OrdinalIgnoreCase)))
            return;

        throw new ForbiddenAccessException("You do not have permission for this action.");
    }

    private static string BuildDeliveryCode(int deliveryId)
        => $"DLV-{deliveryId:D6}";

    private static string BuildOrderCode(int storeOrderId)
        => $"SO-{storeOrderId:D6}";
}