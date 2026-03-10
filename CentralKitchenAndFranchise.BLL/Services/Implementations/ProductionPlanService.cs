using CentralKitchenAndFranchise.BLL.Exceptions;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.Enums;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.ProductionPlans;
using CentralKitchenAndFranchise.DTO.Responses.ProductionPlans;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations
{
    public class ProductionPlanService : IProductionPlanService
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUserService _current;
        private readonly IFranchiseAccessService _access;

        public ProductionPlanService(AppDbContext db, ICurrentUserService current, IFranchiseAccessService access)
        {
            _db = db;
            _current = current;
            _access = access;
        }

        // BR-31 + BR-32: Generate plan theo PlanDate (cycle=ngày) từ TẤT CẢ đơn LOCKED
        // của các franchise thuộc CentralKitchen
        public async Task<ProductionPlanResponse> CreateAsync(
            int centralKitchenId,
            CreateProductionPlanDto request,
            CancellationToken ct = default)
        {
            if (request.PlanDate == default)
                throw new ArgumentException("PlanDate is required.");

            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

            var exists = await _db.ProductionPlans
                .AsNoTracking()
                .AnyAsync(x => x.CentralKitchenId == centralKitchenId && x.PlanDate == request.PlanDate, ct);

            if (exists)
                throw new InvalidOperationException("Production plan already exists for this date.");

            // Lấy các franchise thuộc central kitchen
            var franchiseIds = await _db.Franchises
                .AsNoTracking()
                .Where(f => f.CentralKitchenId == centralKitchenId)
                .Select(f => f.FranchiseId)
                .ToListAsync(ct);

            if (franchiseIds.Count == 0)
                throw new InvalidOperationException("No franchises found for this central kitchen.");

            // BR-31: lấy TẤT CẢ đơn LOCKED của ngày đó thuộc các franchise do CK quản lý
            var lockedOrders = await _db.StoreOrders
                .AsNoTracking()
                .Where(o =>
                    o.OrderDate == request.PlanDate &&
                    o.Status == StoreOrderStatus.Locked &&
                    franchiseIds.Contains(o.FranchiseId))
                .Include(o => o.Items)
                .ToListAsync(ct);

            if (lockedOrders.Count == 0)
                throw new InvalidOperationException("No LOCKED orders found for this date.");

            var productMap = lockedOrders
                .SelectMany(o => o.Items)
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var productIds = productMap.Keys.ToList();

            var activeIds = await _db.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.ProductId) && p.Status == ProductStatus.Active)
                .Select(p => p.ProductId)
                .ToListAsync(ct);

            var activeSet = activeIds.ToHashSet();
            var missing = productIds.Where(id => !activeSet.Contains(id)).ToList();

            if (missing.Count > 0)
                throw new InvalidOperationException("Some products are not ACTIVE or not found.");

            var now = DateTime.UtcNow;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var plan = new ProductionPlan
            {
                CentralKitchenId = centralKitchenId,
                PlanDate = request.PlanDate,
                Status = ProductionPlanStatus.DRAFT,
                CreatedAt = now
            };

            _db.ProductionPlans.Add(plan);
            await _db.SaveChangesAsync(ct);

            foreach (var (productId, qty) in productMap)
            {
                _db.ProductionPlanItems.Add(new ProductionPlanItem
                {
                    ProductionPlanId = plan.ProductionPlanId,
                    ProductId = productId,
                    Quantity = qty
                });
            }

            await _db.SaveChangesAsync(ct);

            await AddAuditAsync(
                action: AuditAction.ProductionPlanCreate,
                centralKitchenId: centralKitchenId,
                entityName: "ProductionPlan",
                entityId: plan.ProductionPlanId,
                oldObj: null,
                newObj: new
                {
                    CentralKitchenId = centralKitchenId,
                    plan.PlanDate,
                    Status = plan.Status.ToString(),
                    Items = productMap,
                    LockedOrderIds = lockedOrders.Select(o => o.StoreOrderId).ToList(),
                    FranchiseIds = franchiseIds
                },
                reason: null,
                ct: ct);

            await tx.CommitAsync(ct);

            return await GetByIdAsync(centralKitchenId, plan.ProductionPlanId, ct);
        }

        // BR-33: update status phải ghi log
        public async Task<ProductionPlanResponse> UpdateStatusAsync(
            int centralKitchenId,
            int productionPlanId,
            UpdateProductionPlanStatusDto request,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Status))
                throw new ArgumentException("Status is required.");

            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

            var plan = await _db.ProductionPlans
                .FirstOrDefaultAsync(
                    x => x.ProductionPlanId == productionPlanId &&
                         x.CentralKitchenId == centralKitchenId,
                    ct);

            if (plan is null)
                throw new KeyNotFoundException($"ProductionPlan {productionPlanId} not found.");

            if (!Enum.TryParse<ProductionPlanStatus>(request.Status.Trim(), true, out var newStatus))
                throw new ArgumentException("Invalid status value, must be DRAFT/CONFIRMED/IN_PROGRESS/COMPLETED/CANCELLED.");

            var old = new { Status = plan.Status?.ToString() };

            if (plan.Status is null)
                throw new InvalidOperationException("ProductionPlan status is null (invalid data).");

            EnsureValidTransition(plan.Status.Value, newStatus);

            plan.Status = newStatus;

            await _db.SaveChangesAsync(ct);

            await AddAuditAsync(
                action: AuditAction.ProductionPlanStatusUpdate,
                centralKitchenId: centralKitchenId,
                entityName: "ProductionPlan",
                entityId: plan.ProductionPlanId,
                oldObj: old,
                newObj: new
                {
                    CentralKitchenId = centralKitchenId,
                    Status = plan.Status?.ToString()
                },
                reason: string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                ct: ct);

            return await GetByIdAsync(centralKitchenId, plan.ProductionPlanId, ct);
        }

        public async Task<ProductionPlanResponse> GetByIdAsync(
            int centralKitchenId,
            int productionPlanId,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

            var plan = await _db.ProductionPlans
                .AsNoTracking()
                .Where(x => x.ProductionPlanId == productionPlanId && x.CentralKitchenId == centralKitchenId)
                .Include(x => x.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(ct);

            if (plan is null)
                throw new KeyNotFoundException($"ProductionPlan {productionPlanId} not found.");

            return new ProductionPlanResponse
            {
                ProductionPlanId = plan.ProductionPlanId,
                CentralKitchenId = plan.CentralKitchenId,
                PlanDate = plan.PlanDate,
                Status = plan.Status?.ToString() ?? "UNKNOWN",
                CreatedAt = plan.CreatedAt,
                Items = plan.Items
                    .OrderBy(i => i.ProductId)
                    .Select(i => new ProductionPlanItemResponse
                    {
                        ProductId = i.ProductId,
                        ProductName = i.Product?.Name ?? "(unknown)",
                        Unit = i.Product?.Unit ?? "",
                        Quantity = i.Quantity
                    })
                    .ToList()
            };
        }

        private static void EnsureValidTransition(ProductionPlanStatus oldStatus, ProductionPlanStatus newStatus)
        {
            if (oldStatus == newStatus) return;

            var allowed = oldStatus switch
            {
                ProductionPlanStatus.DRAFT => new[] { ProductionPlanStatus.CONFIRMED, ProductionPlanStatus.CANCELLED },
                ProductionPlanStatus.CONFIRMED => new[] { ProductionPlanStatus.IN_PROGRESS, ProductionPlanStatus.CANCELLED },
                ProductionPlanStatus.IN_PROGRESS => new[] { ProductionPlanStatus.COMPLETED, ProductionPlanStatus.CANCELLED },
                ProductionPlanStatus.COMPLETED => Array.Empty<ProductionPlanStatus>(),
                ProductionPlanStatus.CANCELLED => Array.Empty<ProductionPlanStatus>(),
                _ => Array.Empty<ProductionPlanStatus>()
            };

            if (!allowed.Contains(newStatus))
                throw new InvalidOperationException($"Invalid status transition: {oldStatus} -> {newStatus}");
        }

        private async Task AddAuditAsync(
            string action,
            int centralKitchenId,
            string entityName,
            int entityId,
            object? oldObj,
            object? newObj,
            string? reason,
            CancellationToken ct)
        {
            var log = new AuditLog
            {
                UserId = _current.UserId,
                FranchiseId = null,
                CentralKitchenId = centralKitchenId,
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
    }
}