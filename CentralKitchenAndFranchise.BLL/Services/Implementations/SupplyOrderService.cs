using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Extensions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.StoreOrders;
using CentralKitchenAndFranchise.DTO.Responses.StoreOrders;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class SupplyOrderService : ISupplyOrderService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFranchiseAccessService _access;

    public SupplyOrderService(
        AppDbContext db,
        ICurrentUserService current,
        IFranchiseAccessService access)
    {
        _db = db;
        _current = current;
        _access = access;
    }

    public async Task<List<SupplyOrderQueueItemResponse>> GetQueueAsync(CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.SupplyCoordinator);

        // Với SupplyCoordinator: chỉ xem queue thuộc đúng CentralKitchen đang được assign.
        // Với Admin/Manager: có thể xem toàn cục.
        int? scopedCentralKitchenId = null;
        if (_current.IsInRole(RoleNames.SupplyCoordinator))
        {
            scopedCentralKitchenId = await _access.GetCurrentAssignedCentralKitchenIdAsync(ct);
        }

        var statuses = new[]
        {
            StoreOrderStatus.ForwardedToSupply,
            StoreOrderStatus.Preparing,
            StoreOrderStatus.ReadyToDeliver,
            StoreOrderStatus.InTransit
        };

        var query = _db.StoreOrders
            .AsNoTracking()
            .Include(x => x.Franchise)
            .Include(x => x.Items)
                .ThenInclude(i => i.Product)
            .Where(x => statuses.Contains(x.Status));

        if (scopedCentralKitchenId.HasValue)
        {
            query = query.Where(x => x.Franchise.CentralKitchenId == scopedCentralKitchenId.Value);
        }

        var orders = await query
            .OrderByDescending(x => x.ForwardedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.StoreOrderId)
            .ToListAsync(ct);

        var userIds = orders
            .Where(x => x.ForwardedByUserId.HasValue)
            .Select(x => x.ForwardedByUserId!.Value)
            .Distinct()
            .ToList();

        var userMap = userIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Users
                .AsNoTracking()
                .Where(x => userIds.Contains(x.UserId))
                .ToDictionaryAsync(x => x.UserId, x => x.Username, ct);

        return orders.Select(x => new SupplyOrderQueueItemResponse
        {
            StoreOrderId = x.StoreOrderId,
            OrderCode = BuildOrderCode(x.StoreOrderId),
            Status = x.Status,
            CreatedAt = x.CreatedAt,
            RequestedDeliveryDate = x.OrderDate,
            StoreId = x.FranchiseId,
            StoreName = x.Franchise.Name,
            TotalItems = x.Items.Count,
            TotalQuantity = x.Items.Sum(i => i.Quantity),
            ForwardedAt = x.ForwardedAt,
            ForwardedBy = x.ForwardedByUserId.HasValue && userMap.TryGetValue(x.ForwardedByUserId.Value, out var username)
                ? username
                : null,
            ProcessingNote = x.ProcessingNote,
            ForwardNote = x.ForwardNote,
            Items = x.Items
                .OrderBy(i => i.ProductId)
                .Select(i => new SupplyOrderQueueItemLineResponse
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? "(unknown)",
                    Sku = i.Product?.Sku,
                    Unit = i.Product?.Unit ?? "",
                    Quantity = i.Quantity
                })
                .ToList()
        }).ToList();
    }

    public async Task<OrderWorkflowActionResponse> PrepareDeliveryAsync(
    int orderId,
    PrepareDeliveryRequest request,
    CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.SupplyCoordinator);

        var order = await LoadManagedOrderAsync(orderId, ct);

        if (!string.Equals(order.Status, StoreOrderStatus.ForwardedToSupply, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only FORWARDED_TO_SUPPLY orders can be prepared.");

        if (!order.Items.Any())
            throw new InvalidOperationException("Cannot prepare delivery for an empty order.");

        await EnsureCentralKitchenHasSufficientProductStockAsync(order, ct);

        var now = DateTime.UtcNow;
        var currentUserId = _current.UserId;
        var oldStatus = order.Status;

        order.Status = StoreOrderStatus.Preparing;
        order.PreparedAt = now;
        order.PreparedByUserId = currentUserId;
        order.PreparingNote = string.IsNullOrWhiteSpace(request?.PreparingNote)
            ? null
            : request.PreparingNote.Trim();

        await EnsureDeliveryArtifactsAsync(order, ct);

        _db.Set<StoreOrderHistory>().Add(new StoreOrderHistory
        {
            StoreOrderId = order.StoreOrderId,
            ActionType = StoreOrderHistoryActions.OrderPreparing,
            ActionLabel = "Supply bắt đầu chuẩn bị giao hàng",
            OldStatus = oldStatus,
            NewStatus = order.Status,
            Note = order.PreparingNote,
            PerformedByUserId = currentUserId,
            PerformedAt = now
        });

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            FranchiseId = order.FranchiseId,
            CentralKitchenId = order.Franchise.CentralKitchenId,
            Action = "STORE_ORDER_PREPARED",
            EntityName = "StoreOrder",
            EntityId = order.StoreOrderId,
            OldDataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                Status = oldStatus
            }),
            NewDataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                order.Status,
                order.PreparedAt,
                order.PreparedByUserId,
                order.PreparingNote
            }),
            Reason = order.PreparingNote,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        return new OrderWorkflowActionResponse
        {
            StoreOrderId = order.StoreOrderId,
            Status = order.Status,
            PreparedAt = order.PreparedAt,
            PreparedBy = await ResolveUsernameAsync(order.PreparedByUserId, ct),
            PreparingNote = order.PreparingNote,
            Message = "Delivery preparation started successfully."
        };
    }

    public async Task<OrderWorkflowActionResponse> UpdateDeliveryStatusAsync(
        int orderId,
        UpdateSupplyDeliveryStatusRequest request,
        CancellationToken ct = default)
    {
        RequireOneOf(RoleNames.Admin, RoleNames.Manager, RoleNames.SupplyCoordinator);

        var order = await LoadManagedOrderAsync(orderId, ct);

        var nextStatus = NormalizeDeliveryStatus(request.Status);
        ValidateDeliveryTransition(order.Status, nextStatus);

        var now = DateTime.UtcNow;
        var currentUserId = _current.UserId;
        var oldStatus = order.Status;

        order.Status = nextStatus;
        order.DeliveryStatusUpdatedAt = now;
        order.DeliveryStatusUpdatedByUserId = currentUserId;
        order.DeliveryStatusNote = string.IsNullOrWhiteSpace(request.StatusNote)
            ? null
            : request.StatusNote.Trim();

        var deliveryPlan = await _db.DeliveryPlans
            .FirstOrDefaultAsync(x => x.StoreOrderId == order.StoreOrderId, ct);

        if (deliveryPlan is null)
            throw new InvalidOperationException("DeliveryPlan was not found for this store order.");

        var delivery = await _db.Deliveries
            .FirstOrDefaultAsync(x => x.DeliveryPlanId == deliveryPlan.DeliveryPlanId, ct);

        if (delivery is null)
            throw new InvalidOperationException("Delivery was not found for this store order.");

        // Sync shipment status xuống aggregate delivery để receiving module còn query được.
        delivery.Status = MapOrderStatusToDeliveryStatus(order.Status);

        if (string.Equals(order.Status, StoreOrderStatus.Delivered, StringComparison.OrdinalIgnoreCase))
        {
            delivery.DeliveredAt = now;
        }

        _db.Set<StoreOrderHistory>().Add(new StoreOrderHistory
        {
            StoreOrderId = order.StoreOrderId,
            ActionType = StoreOrderHistoryActions.DeliveryStatusChanged,
            ActionLabel = $"Cập nhật trạng thái giao hàng: {nextStatus}",
            OldStatus = oldStatus,
            NewStatus = order.Status,
            Note = order.DeliveryStatusNote,
            PerformedByUserId = currentUserId,
            PerformedAt = now
        });

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            FranchiseId = order.FranchiseId,
            CentralKitchenId = order.Franchise.CentralKitchenId,
            Action = "STORE_ORDER_DELIVERY_STATUS_UPDATED",
            EntityName = "StoreOrder",
            EntityId = order.StoreOrderId,
            OldDataJson = JsonSerializer.Serialize(new
            {
                Status = oldStatus
            }),
            NewDataJson = JsonSerializer.Serialize(new
            {
                order.Status,
                order.DeliveryStatusUpdatedAt,
                order.DeliveryStatusUpdatedByUserId,
                order.DeliveryStatusNote,
                DeliveryStatus = delivery.Status,
                delivery.DeliveredAt
            }),
            Reason = order.DeliveryStatusNote,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        return new OrderWorkflowActionResponse
        {
            StoreOrderId = order.StoreOrderId,
            Status = order.Status,
            UpdatedAt = order.DeliveryStatusUpdatedAt,
            UpdatedBy = await ResolveUsernameAsync(order.DeliveryStatusUpdatedByUserId, ct),
            StatusNote = order.DeliveryStatusNote,
            Message = "Delivery status updated successfully."
        };
    }

    /// <summary>
    /// Load order + enforce scope:
    /// - Admin/Manager: pass
    /// - SupplyCoordinator: chỉ được thao tác trên order thuộc CK đang assign
    /// </summary>
    private async Task<StoreOrder> LoadManagedOrderAsync(int orderId, CancellationToken ct)
    {
        var order = await _db.StoreOrders
            .Include(x => x.Franchise)
            .Include(x => x.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.StoreOrderId == orderId, ct);

        if (order is null)
            throw new KeyNotFoundException($"StoreOrder {orderId} not found.");

        await _access.EnsureCanAccessCentralKitchenAsync(order.Franchise.CentralKitchenId, ct);

        return order;
    }

    private async Task EnsureCentralKitchenHasSufficientProductStockAsync(StoreOrder order, CancellationToken ct)
    {
        var requiredMap = order.Items
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var productBatches = await _db.ProductBatches
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x =>
                x.CentralKitchenId == order.Franchise.CentralKitchenId &&
                x.FranchiseId == null &&
                requiredMap.Keys.Contains(x.ProductId) &&
                x.Quantity > 0)
            .ToListAsync(ct);

        var shortages = new List<string>();

        foreach (var entry in requiredMap.OrderBy(x => x.Key))
        {
            var productId = entry.Key;
            var requiredQty = entry.Value;

            var availableQty = productBatches
                .Where(x => x.ProductId == productId)
                .OrderBy(x => x.CalculateExpiredAt() == null)
                .ThenBy(x => x.CalculateExpiredAt())
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.BatchId)
                .Sum(x => x.Quantity);

            if (availableQty >= requiredQty)
                continue;

            var productName = order.Items
                .Where(x => x.ProductId == productId)
                .Select(x => x.Product?.Name)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                ?? $"ProductId={productId}";

            shortages.Add($"{productName}: required={requiredQty}, available={availableQty}");
        }

        if (shortages.Count > 0)
        {
            throw new InvalidOperationException(
                "Insufficient central kitchen inventory to prepare this delivery. " +
                string.Join("; ", shortages));
        }
    }
    /// <summary>
    /// Phase 1:
    /// - 1 StoreOrder -> 1 DeliveryPlan -> 1 Delivery
    /// - sync product items từ StoreOrder.Items
    /// </summary>
    private async Task EnsureDeliveryArtifactsAsync(StoreOrder order, CancellationToken ct)
    {
        var existingPlan = await _db.DeliveryPlans
            .FirstOrDefaultAsync(x => x.StoreOrderId == order.StoreOrderId, ct);

        if (existingPlan is null)
        {
            existingPlan = new DeliveryPlan
            {
                CentralKitchenId = order.Franchise.CentralKitchenId,
                FranchiseId = order.FranchiseId,
                PlannedDate = order.OrderDate,
                StoreOrderId = order.StoreOrderId
            };

            _db.DeliveryPlans.Add(existingPlan);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            // Giữ plan luôn đúng scope/order date mới nhất nếu có thay đổi dữ liệu cũ
            existingPlan.CentralKitchenId = order.Franchise.CentralKitchenId;
            existingPlan.FranchiseId = order.FranchiseId;
            existingPlan.PlannedDate = order.OrderDate;
        }

        var existingDelivery = await _db.Deliveries
            .Include(x => x.ProductItems)
            .FirstOrDefaultAsync(x => x.DeliveryPlanId == existingPlan.DeliveryPlanId, ct);

        if (existingDelivery is null)
        {
            existingDelivery = new Delivery
            {
                DeliveryPlanId = existingPlan.DeliveryPlanId,
                FromCentralKitchenId = order.Franchise.CentralKitchenId,
                Status = DeliveryStatus.Created,
                CreatedAt = DateTime.UtcNow
            };

            _db.Deliveries.Add(existingDelivery);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            // Fix dữ liệu cũ: luôn đảm bảo source CK được set đúng
            existingDelivery.FromCentralKitchenId = order.Franchise.CentralKitchenId;
        }

        var orderItemMap = order.Items
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var existingItemMap = existingDelivery.ProductItems
            .ToDictionary(x => x.ProductId, x => x);

        // Upsert item từ StoreOrder -> DeliveryProductItem
        foreach (var (productId, qty) in orderItemMap)
        {
            if (existingItemMap.TryGetValue(productId, out var line))
            {
                line.Quantity = qty;
            }
            else
            {
                existingDelivery.ProductItems.Add(new DeliveryProductItem
                {
                    ProductId = productId,
                    Quantity = qty
                });
            }
        }

        // Xóa orphan item nếu có lệch dữ liệu cũ
        var orphanLines = existingDelivery.ProductItems
            .Where(x => !orderItemMap.ContainsKey(x.ProductId))
            .ToList();

        if (orphanLines.Count > 0)
        {
            _db.DeliveryProductItems.RemoveRange(orphanLines);
        }
    }

    private static string NormalizeDeliveryStatus(string rawStatus)
    {
        var value = (rawStatus ?? string.Empty).Trim().ToUpperInvariant();

        return value switch
        {
            var s when s == StoreOrderStatus.Preparing => StoreOrderStatus.Preparing,
            var s when s == StoreOrderStatus.ReadyToDeliver => StoreOrderStatus.ReadyToDeliver,
            var s when s == StoreOrderStatus.InTransit => StoreOrderStatus.InTransit,
            var s when s == StoreOrderStatus.Delivered => StoreOrderStatus.Delivered,
            _ => throw new ArgumentException($"Unsupported delivery status: {rawStatus}")
        };
    }

    private static void ValidateDeliveryTransition(string currentStatus, string nextStatus)
    {
        var valid = (currentStatus, nextStatus) switch
        {
            (var s, var n) when s == StoreOrderStatus.ForwardedToSupply && n == StoreOrderStatus.Preparing => true,
            (var s, var n) when s == StoreOrderStatus.Preparing && n == StoreOrderStatus.ReadyToDeliver => true,
            (var s, var n) when s == StoreOrderStatus.ReadyToDeliver && n == StoreOrderStatus.InTransit => true,
            (var s, var n) when s == StoreOrderStatus.InTransit && n == StoreOrderStatus.Delivered => true,
            _ => false
        };

        if (!valid)
            throw new InvalidOperationException($"Invalid delivery status transition: {currentStatus} -> {nextStatus}");
    }

    private static string MapOrderStatusToDeliveryStatus(string orderStatus)
    {
        return orderStatus switch
        {
            var s when s == StoreOrderStatus.Preparing => DeliveryStatus.Created,
            var s when s == StoreOrderStatus.ReadyToDeliver => DeliveryStatus.Created,
            var s when s == StoreOrderStatus.InTransit => DeliveryStatus.Shipped,
            var s when s == StoreOrderStatus.Delivered => DeliveryStatus.Delivered,
            var s when s == StoreOrderStatus.Cancelled => DeliveryStatus.Cancelled,
            _ => DeliveryStatus.Created
        };
    }

    private async Task<string?> ResolveUsernameAsync(int? userId, CancellationToken ct)
    {
        if (!userId.HasValue)
            return null;

        return await _db.Users
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .Select(x => x.Username)
            .FirstOrDefaultAsync(ct);
    }

    private void RequireOneOf(params string[] roles)
    {
        var currentRole = _current.Role;

        if (roles.Any(r => string.Equals(r, currentRole, StringComparison.OrdinalIgnoreCase)))
            return;

        throw new ForbiddenAccessException("You do not have permission for this action.");
    }

    private static string BuildOrderCode(int storeOrderId)
        => $"SO-{storeOrderId:D6}";
}