using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Requests.Inventory;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.Inventory;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Extensions;
using CentralKitchenAndFranchise.DTO.Requests.Ingredients;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations
{
    public class InventoryService : IInventoryService
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUserService _current;
        private readonly IFranchiseAccessService _access;

        public InventoryService(AppDbContext db, ICurrentUserService current, IFranchiseAccessService access)
        {
            _db = db;
            _current = current;
            _access = access;
        }

        // =========================================================
        // INGREDIENT INBOUND
        // =========================================================
        // Logic quan trọng:
        // - ExpiredAt được derive từ CreatedAt + Ingredient.ShelfLifeDays
        // - BatchCode unique theo (IngredientId, BatchCode, FranchiseId)
        public async Task<IngredientInboundResponse> InboundIngredientAsync(
            int franchiseId,
            CreateIngredientInboundDto request,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessAsync(franchiseId, ct);

            if (request.IngredientId <= 0)
                throw new ArgumentException("IngredientId must be positive.");

            if (string.IsNullOrWhiteSpace(request.BatchCode))
                throw new ArgumentException("BatchCode is required.");

            if (request.Quantity <= 0)
                throw new ArgumentException("Quantity must be > 0.");

            var ingredient = await _db.Ingredients
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IngredientId == request.IngredientId, ct);

            if (ingredient is null)
                throw new KeyNotFoundException($"Ingredient {request.IngredientId} not found.");

            var now = DateTime.UtcNow;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var batch = new IngredientBatch
            {
                IngredientId = request.IngredientId,
                Type = InventoryOwnerType.Franchise,
                FranchiseId = franchiseId,
                CentralKitchenId = null,
                BatchCode = request.BatchCode.Trim(),
                Quantity = request.Quantity,
                CreatedAt = now
            };

            _db.IngredientBatches.Add(batch);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                throw new InvalidOperationException("BatchCode already exists for this ingredient in this franchise.");
            }

            var mv = new InventoryMovement
            {
                BatchId = batch.BatchId,
                Type = "IN",
                Quantity = request.Quantity,
                CreatedByUserId = _current.UserId,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                CreatedAt = now
            };

            _db.InventoryMovements.Add(mv);
            await _db.SaveChangesAsync(ct);

            // Gắn ingredient vào batch để dùng helper CalculateExpiredAt()
            batch.Ingredient = ingredient;
            var expiredAt = batch.CalculateExpiredAt();

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = _current.UserId,
                FranchiseId = franchiseId,
                Action = "INGREDIENT_INBOUND_CREATE",
                EntityName = "IngredientBatch",
                EntityId = batch.BatchId,
                OldDataJson = null,
                NewDataJson = JsonSerializer.Serialize(new
                {
                    batch.BatchId,
                    batch.IngredientId,
                    batch.FranchiseId,
                    batch.BatchCode,
                    batch.CreatedAt,
                    ExpiredAt = expiredAt,
                    batch.Quantity,
                    Movement = new
                    {
                        mv.MovementId,
                        mv.Type,
                        mv.Quantity,
                        mv.CreatedAt,
                        mv.Reason
                    }
                }),
                Reason = mv.Reason,
                CreatedAt = now
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new IngredientInboundResponse
            {
                BatchId = batch.BatchId,
                FranchiseId = franchiseId,
                IngredientId = batch.IngredientId,
                BatchCode = batch.BatchCode,
                ExpiredAt = expiredAt,
                Quantity = batch.Quantity,
                CreatedMovementId = mv.MovementId,
                CreatedAt = mv.CreatedAt
            };
        }

        // =========================================================
        // ISSUE INGREDIENTS FOR PRODUCTION PLAN
        // =========================================================
        // Logic quan trọng:
        // - Chỉ issue từ tồn kho CentralKitchen
        // - Tính nhu cầu từ ProductionPlan + BOM ACTIVE version mới nhất
        // - FEFO dùng ExpiredAt derived, không dùng cột DB
        // - Vì ExpiredAt là derived nên phải Include Ingredient rồi sort in-memory
        public async Task<IssueIngredientsByProductionPlanResponse> IssueIngredientsByProductionPlanAsync(
            int centralKitchenId,
            int productionPlanId,
            IssueIngredientsByProductionPlanDto request,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

            var plan = await _db.ProductionPlans
                .AsNoTracking()
                .Include(p => p.Items)
                .FirstOrDefaultAsync(
                    p => p.ProductionPlanId == productionPlanId &&
                         p.CentralKitchenId == centralKitchenId,
                    ct);

            if (plan is null)
                throw new KeyNotFoundException($"ProductionPlan {productionPlanId} not found.");

            if (plan.Items.Count == 0)
                throw new InvalidOperationException("Production plan has no items.");

            // 1) Lấy ACTIVE BOM version mới nhất cho từng product trong plan
            var productIds = plan.Items.Select(i => i.ProductId).Distinct().ToList();

            var boms = await _db.Boms
                .AsNoTracking()
                .Where(b => productIds.Contains(b.ProductId) && b.Status == "ACTIVE")
                .ToListAsync(ct);

            var bomByProduct = boms
                .GroupBy(b => b.ProductId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Version).First());

            var missingBomProducts = productIds
                .Where(pid => !bomByProduct.ContainsKey(pid))
                .ToList();

            if (missingBomProducts.Count > 0)
                throw new InvalidOperationException(
                    $"Missing ACTIVE BOM for products: {string.Join(",", missingBomProducts)}");

            var bomIds = bomByProduct.Values.Select(b => b.BomId).Distinct().ToList();

            var bomItems = await _db.BomItems
                .AsNoTracking()
                .Where(x => bomIds.Contains(x.BomId))
                .ToListAsync(ct);

            // 2) Tính tổng nguyên liệu cần dùng
            var bomItemMap = bomItems
                .GroupBy(x => x.BomId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var required = new Dictionary<int, decimal>(); // IngredientId -> Qty

            foreach (var pi in plan.Items)
            {
                var bom = bomByProduct[pi.ProductId];

                if (!bomItemMap.TryGetValue(bom.BomId, out var items) || items.Count == 0)
                    throw new InvalidOperationException($"BOM {bom.BomId} has no items.");

                foreach (var bi in items)
                {
                    var need = pi.Quantity * bi.Quantity;
                    if (need <= 0) continue;

                    required.TryGetValue(bi.IngredientId, out var cur);
                    required[bi.IngredientId] = cur + need;
                }
            }

            if (required.Count == 0)
                throw new InvalidOperationException("No ingredient requirements computed (check BOM quantities).");

            var ingIds = required.Keys.ToList();

            var ingNameMap = await _db.Ingredients
                .AsNoTracking()
                .Where(i => ingIds.Contains(i.IngredientId))
                .Select(i => new { i.IngredientId, i.Name })
                .ToDictionaryAsync(x => x.IngredientId, x => x.Name, ct);

            var now = DateTime.UtcNow;
            var reason = string.IsNullOrWhiteSpace(request.Reason)
                ? $"Issue ingredients for ProductionPlan {productionPlanId}"
                : request.Reason.Trim();

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var response = new IssueIngredientsByProductionPlanResponse
            {
                ProductionPlanId = productionPlanId,
                CentralKitchenId = centralKitchenId,
                PlanDate = plan.PlanDate,
                IssuedAt = now
            };

            foreach (var (ingredientId, requiredQty) in required.OrderBy(x => x.Key))
            {
                // Load tracking batches + Include Ingredient để derive ExpiredAt
                var batches = await _db.IngredientBatches
                    .Include(b => b.Ingredient)
                    .Where(b =>
                        b.Type == InventoryOwnerType.CentralKitchen &&
                        b.CentralKitchenId == centralKitchenId &&
                        b.IngredientId == ingredientId &&
                        b.Quantity > 0)
                    .ToListAsync(ct);

                // FEFO theo expiry derived.
                // Nếu ExpiredAt null thì đẩy xuống cuối.
                batches = batches
                    .OrderBy(b => b.CalculateExpiredAt() == null)
                    .ThenBy(b => b.CalculateExpiredAt())
                    .ThenBy(b => b.CreatedAt)
                    .ThenBy(b => b.BatchId)
                    .ToList();

                var available = batches.Sum(b => b.Quantity);
                if (available < requiredQty)
                {
                    throw new InvalidOperationException(
                        $"Insufficient inventory for IngredientId={ingredientId}. Required={requiredQty}, Available={available}.");
                }

                var line = new IssuedIngredientLine
                {
                    IngredientId = ingredientId,
                    IngredientName = ingNameMap.TryGetValue(ingredientId, out var n) ? n : "(unknown)",
                    RequiredQuantity = requiredQty
                };

                var remaining = requiredQty;

                foreach (var batch in batches)
                {
                    if (remaining <= 0) break;

                    var take = batch.Quantity >= remaining ? remaining : batch.Quantity;
                    if (take <= 0) continue;

                    batch.Quantity -= take;

                    var mv = new InventoryMovement
                    {
                        BatchId = batch.BatchId,
                        Type = "OUT",
                        Quantity = take,
                        CreatedByUserId = _current.UserId,
                        Reason = reason,
                        CreatedAt = now
                    };

                    _db.InventoryMovements.Add(mv);
                    await _db.SaveChangesAsync(ct); // cần MovementId cho response

                    line.Batches.Add(new IssuedBatchLine
                    {
                        BatchId = batch.BatchId,
                        BatchCode = batch.BatchCode,
                        ExpiredAt = batch.CalculateExpiredAt(),
                        IssuedQuantity = take,
                        MovementId = mv.MovementId
                    });

                    remaining -= take;
                }

                response.Lines.Add(line);
            }

            await _db.SaveChangesAsync(ct);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = _current.UserId,
                CentralKitchenId = centralKitchenId,
                Action = "INGREDIENT_ISSUE_BY_PRODUCTION_PLAN",
                EntityName = "ProductionPlan",
                EntityId = productionPlanId,
                OldDataJson = null,
                NewDataJson = JsonSerializer.Serialize(new
                {
                    ProductionPlanId = productionPlanId,
                    plan.PlanDate,
                    Reason = reason,
                    Required = required,
                    Issued = response.Lines.Select(l => new
                    {
                        l.IngredientId,
                        l.RequiredQuantity,
                        Batches = l.Batches.Select(b => new
                        {
                            b.BatchId,
                            b.BatchCode,
                            b.ExpiredAt,
                            b.IssuedQuantity,
                            b.MovementId
                        })
                    })
                }),
                Reason = reason,
                CreatedAt = now
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return response;
        }

        // =========================================================
        // ADJUST / WASTE INGREDIENT INVENTORY
        // =========================================================
        // Logic quan trọng:
        // - Chỉ adjust batch ingredient thuộc franchise hiện tại
        // - Phải Include Ingredient vì response/audit cần ExpiredAt derived
        public async Task<AdjustIngredientInventoryResponse> AdjustIngredientAsync(
            int franchiseId,
            AdjustIngredientInventoryDto request,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessAsync(franchiseId, ct);

            if (request.BatchId <= 0)
                throw new ArgumentException("BatchId must be positive.");

            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new ArgumentException("Reason is required.");

            if (request.DeltaQuantity == 0)
                throw new ArgumentException("DeltaQuantity must not be 0.");

            var type = (request.Type ?? "").Trim().ToUpperInvariant();
            if (type is not ("ADJUST" or "WASTE"))
                throw new ArgumentException("Type must be ADJUST or WASTE.");

            if (type == "WASTE" && request.DeltaQuantity >= 0)
                throw new ArgumentException("WASTE requires DeltaQuantity < 0.");

            var batch = await _db.IngredientBatches
                .Include(b => b.Ingredient)
                .FirstOrDefaultAsync(b =>
                    b.BatchId == request.BatchId &&
                    b.Type == InventoryOwnerType.Franchise &&
                    b.FranchiseId == franchiseId,
                    ct);

            if (batch is null)
                throw new KeyNotFoundException($"IngredientBatch {request.BatchId} not found.");

            var before = batch.Quantity;
            var after = before + request.DeltaQuantity;

            if (after < 0)
                throw new InvalidOperationException("Adjustment would make inventory negative.");

            var now = DateTime.UtcNow;
            var expiredAt = batch.CalculateExpiredAt();

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            batch.Quantity = after;
            await _db.SaveChangesAsync(ct);

            var mv = new InventoryMovement
            {
                BatchId = batch.BatchId,
                Type = type,
                Quantity = Math.Abs(request.DeltaQuantity),
                CreatedByUserId = _current.UserId,
                Reason = BuildReason(request.Reason, request.Reference),
                CreatedAt = now
            };

            _db.InventoryMovements.Add(mv);
            await _db.SaveChangesAsync(ct);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = _current.UserId,
                FranchiseId = franchiseId,
                Action = type == "WASTE" ? "INGREDIENT_WASTE" : "INGREDIENT_ADJUST",
                EntityName = "IngredientBatch",
                EntityId = batch.BatchId,
                OldDataJson = JsonSerializer.Serialize(new
                {
                    batch.BatchId,
                    batch.IngredientId,
                    batch.BatchCode,
                    batch.CreatedAt,
                    ExpiredAt = expiredAt,
                    Quantity = before
                }),
                NewDataJson = JsonSerializer.Serialize(new
                {
                    batch.BatchId,
                    batch.IngredientId,
                    batch.BatchCode,
                    batch.CreatedAt,
                    ExpiredAt = expiredAt,
                    Quantity = after,
                    Movement = new
                    {
                        mv.MovementId,
                        mv.Type,
                        Delta = request.DeltaQuantity,
                        mv.CreatedAt,
                        mv.Reason
                    }
                }),
                Reason = mv.Reason,
                CreatedAt = now
            });

            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            return new AdjustIngredientInventoryResponse
            {
                BatchId = batch.BatchId,
                MovementId = mv.MovementId,
                FranchiseId = franchiseId,
                IngredientId = batch.IngredientId,
                BatchCode = batch.BatchCode,
                ExpiredAt = expiredAt,
                BeforeQuantity = before,
                DeltaQuantity = request.DeltaQuantity,
                AfterQuantity = after,
                Type = type,
                Reason = mv.Reason ?? "",
                CreatedAt = now
            };
        }

        private static string BuildReason(string reason, string? reference)
        {
            reason = reason.Trim();
            if (string.IsNullOrWhiteSpace(reference)) return reason;
            return $"{reason} (ref: {reference.Trim()})";
        }

        // =========================================================
        // PRODUCT INBOUND
        // =========================================================
        // Phần product vẫn đang dùng ExpiredAt persisted trên ProductBatch,
        // chưa chuyển sang flow derived như ingredient.
        public async Task<ProductInboundResponse> InboundProductAsync(
    int franchiseId,
    CreateProductInboundDto request,
    CancellationToken ct = default)
        {
            await _access.EnsureCanAccessAsync(franchiseId, ct);

            if (request.ProductId <= 0)
                throw new ArgumentException("ProductId must be positive.");

            if (string.IsNullOrWhiteSpace(request.BatchCode))
                throw new ArgumentException("BatchCode is required.");

            if (request.Quantity <= 0)
                throw new ArgumentException("Quantity must be > 0.");

            var product = await _db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProductId == request.ProductId, ct);

            if (product is null)
                throw new KeyNotFoundException($"Product {request.ProductId} not found.");

            if (!string.Equals(product.Status, ProductStatus.Active, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Product {request.ProductId} is not ACTIVE.");

            if (product.ShelfLifeDays <= 0)
                throw new InvalidOperationException(
                    $"Product {request.ProductId} has invalid ShelfLifeDays={product.ShelfLifeDays}. Product master data must be fixed.");

            var now = DateTime.UtcNow;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var batch = new ProductBatch
            {
                ProductId = request.ProductId,
                FranchiseId = franchiseId,
                CentralKitchenId = null,
                BatchCode = request.BatchCode.Trim(),
                Quantity = request.Quantity,
                CreatedAt = now
            };

            // gắn navigation để helper derive expiry hoạt động
            batch.Product = product;

            _db.ProductBatches.Add(batch);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                throw new InvalidOperationException("BatchCode already exists for this product in this franchise.");
            }

            var mv = new ProductMovement
            {
                BatchId = batch.BatchId,
                Type = MovementType.In,
                Quantity = request.Quantity,
                CreatedByUserId = _current.UserId,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                DeliveryId = null,
                CreatedAt = now
            };

            _db.ProductMovements.Add(mv);
            await _db.SaveChangesAsync(ct);

            var expiredAt = batch.CalculateExpiredAt();

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = _current.UserId,
                FranchiseId = franchiseId,
                Action = "PRODUCT_INBOUND_CREATE",
                EntityName = "ProductBatch",
                EntityId = batch.BatchId,
                OldDataJson = null,
                NewDataJson = JsonSerializer.Serialize(new
                {
                    batch.BatchId,
                    batch.ProductId,
                    batch.FranchiseId,
                    batch.BatchCode,
                    batch.CreatedAt,
                    ExpiredAt = expiredAt,
                    batch.Quantity,
                    Movement = new
                    {
                        mv.MovementId,
                        mv.Type,
                        mv.Quantity,
                        mv.CreatedAt,
                        mv.Reason
                    }
                }),
                Reason = mv.Reason,
                CreatedAt = now
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new ProductInboundResponse
            {
                BatchId = batch.BatchId,
                FranchiseId = franchiseId,
                ProductId = batch.ProductId,
                BatchCode = batch.BatchCode,
                ExpiredAt = expiredAt,
                Quantity = batch.Quantity,
                CreatedMovementId = mv.MovementId,
                CreatedAt = mv.CreatedAt
            };
        }

        public async Task<PagedResult<StoreIngredientInventoryResponse>> GetStoreIngredientInventoryAsync(
    int franchiseId,
    InventoryListQuery query,
    CancellationToken ct = default)
        {
            await _access.EnsureCanAccessAsync(franchiseId, ct);

            query ??= new InventoryListQuery();

            var page = query.Page <= 0 ? 1 : query.Page;
            var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
            if (pageSize > 200) pageSize = 200;

            var sortBy = (query.SortBy ?? "name").Trim().ToLowerInvariant();
            var sortDir = (query.SortDir ?? "asc").Trim().ToLowerInvariant();
            if (sortDir is not ("asc" or "desc"))
                throw new ArgumentException("sortDir must be asc or desc.");

            IQueryable<IngredientBatch> batchQuery = _db.IngredientBatches
                .AsNoTracking()
                .Include(x => x.Ingredient)
                .Where(x => x.FranchiseId == franchiseId);

            if (query.OnlyPositive != false)
                batchQuery = batchQuery.Where(x => x.Quantity > 0);

            if (query.ExpireFrom.HasValue)
                batchQuery = batchQuery.Where(x => x.ExpiredAt >= query.ExpireFrom.Value);

            if (query.ExpireTo.HasValue)
                batchQuery = batchQuery.Where(x => x.ExpiredAt <= query.ExpireTo.Value);

            if (query.NearExpiryOnly == true)
            {
                var nearExpiryDays = await GetIntSettingAsync(SettingKeys.NearExpiryDays, 7, ct);
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var end = today.AddDays(nearExpiryDays);

                batchQuery = batchQuery.Where(x =>
                    x.ExpiredAt.HasValue &&
                    x.ExpiredAt.Value >= today &&
                    x.ExpiredAt.Value <= end);
            }

            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var term = query.Q.Trim();
                batchQuery = batchQuery.Where(x =>
                    EF.Functions.ILike(x.Ingredient.Name, $"%{term}%") ||
                    EF.Functions.ILike(x.BatchCode, $"%{term}%"));
            }

            var grouped = await batchQuery
                .GroupBy(x => new { x.IngredientId, x.Ingredient.Name, x.Ingredient.Unit })
                .Select(g => new StoreIngredientInventoryResponse
                {
                    IngredientId = g.Key.IngredientId,
                    IngredientName = g.Key.Name,
                    Unit = g.Key.Unit,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    EarliestExpiry = g
                        .Where(x => x.ExpiredAt.HasValue)
                        .Select(x => x.ExpiredAt)
                        .OrderBy(x => x)
                        .FirstOrDefault(),
                    Batches = g
                        .OrderBy(x => x.ExpiredAt == null)
                        .ThenBy(x => x.ExpiredAt)
                        .ThenBy(x => x.BatchId)
                        .Select(x => new StoreIngredientInventoryBatchResponse
                        {
                            BatchId = x.BatchId,
                            BatchCode = x.BatchCode,
                            Quantity = x.Quantity,
                            ExpiredAt = x.ExpiredAt
                        })
                        .ToList()
                })
                .ToListAsync(ct);

            IEnumerable<StoreIngredientInventoryResponse> result = grouped;

            result = (sortBy, sortDir) switch
            {
                ("quantity", "asc") => result.OrderBy(x => x.TotalQuantity).ThenBy(x => x.IngredientName),
                ("quantity", "desc") => result.OrderByDescending(x => x.TotalQuantity).ThenBy(x => x.IngredientName),
                ("expiry", "asc") => result.OrderBy(x => x.EarliestExpiry.HasValue ? 0 : 1).ThenBy(x => x.EarliestExpiry).ThenBy(x => x.IngredientName),
                ("expiry", "desc") => result.OrderByDescending(x => x.EarliestExpiry.HasValue ? 0 : 1).ThenByDescending(x => x.EarliestExpiry).ThenBy(x => x.IngredientName),
                ("name", "desc") => result.OrderByDescending(x => x.IngredientName),
                _ => result.OrderBy(x => x.IngredientName)
            };

            var total = result.Count();

            var pagedItems = result
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return PagedResult<StoreIngredientInventoryResponse>.Create(
                pagedItems,
                page,
                pageSize,
                total);
        }

        public async Task<PagedResult<StoreProductInventoryResponse>> GetStoreProductInventoryAsync(
    int franchiseId,
    InventoryListQuery query,
    CancellationToken ct = default)
        {
            await _access.EnsureCanAccessAsync(franchiseId, ct);

            query ??= new InventoryListQuery();

            var page = query.Page <= 0 ? 1 : query.Page;
            var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
            if (pageSize > 200) pageSize = 200;

            var sortBy = (query.SortBy ?? "name").Trim().ToLowerInvariant();
            var sortDir = (query.SortDir ?? "asc").Trim().ToLowerInvariant();
            if (sortDir is not ("asc" or "desc"))
                throw new ArgumentException("sortDir must be asc or desc.");

            IQueryable<ProductBatch> batchQuery = _db.ProductBatches
                .AsNoTracking()
                .Include(x => x.Product)
                .Where(x => x.FranchiseId == franchiseId);

            if (query.OnlyPositive != false)
                batchQuery = batchQuery.Where(x => x.Quantity > 0);

            if (query.ExpireFrom.HasValue)
                batchQuery = batchQuery.Where(x => x.ExpiredAt >= query.ExpireFrom.Value);

            if (query.ExpireTo.HasValue)
                batchQuery = batchQuery.Where(x => x.ExpiredAt <= query.ExpireTo.Value);

            if (query.NearExpiryOnly == true)
            {
                var nearExpiryDays = await GetIntSettingAsync(SettingKeys.NearExpiryDays, 7, ct);
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var end = today.AddDays(nearExpiryDays);

                batchQuery = batchQuery.Where(x =>
                    x.ExpiredAt.HasValue &&
                    x.ExpiredAt.Value >= today &&
                    x.ExpiredAt.Value <= end);
            }

            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var term = query.Q.Trim();
                batchQuery = batchQuery.Where(x =>
                    EF.Functions.ILike(x.Product.Name, $"%{term}%") ||
                    EF.Functions.ILike(x.Product.Sku, $"%{term}%") ||
                    EF.Functions.ILike(x.BatchCode, $"%{term}%"));
            }

            var grouped = await batchQuery
                .GroupBy(x => new { x.ProductId, x.Product.Name, x.Product.Sku, x.Product.Unit, x.Product.ProductType })
                .Select(g => new StoreProductInventoryResponse
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.Name,
                    Sku = g.Key.Sku,
                    Unit = g.Key.Unit,
                    ProductType = g.Key.ProductType,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    EarliestExpiry = g
                        .Where(x => x.ExpiredAt.HasValue)
                        .Select(x => x.ExpiredAt)
                        .OrderBy(x => x)
                        .FirstOrDefault(),
                    Batches = g
                        .OrderBy(x => x.ExpiredAt == null)
                        .ThenBy(x => x.ExpiredAt)
                        .ThenBy(x => x.BatchId)
                        .Select(x => new StoreProductInventoryBatchResponse
                        {
                            BatchId = x.BatchId,
                            BatchCode = x.BatchCode,
                            Quantity = x.Quantity,
                            ExpiredAt = x.ExpiredAt
                        })
                        .ToList()
                })
                .ToListAsync(ct);

            IEnumerable<StoreProductInventoryResponse> result = grouped;

            result = (sortBy, sortDir) switch
            {
                ("quantity", "asc") => result.OrderBy(x => x.TotalQuantity).ThenBy(x => x.ProductName),
                ("quantity", "desc") => result.OrderByDescending(x => x.TotalQuantity).ThenBy(x => x.ProductName),
                ("expiry", "asc") => result.OrderBy(x => x.EarliestExpiry.HasValue ? 0 : 1).ThenBy(x => x.EarliestExpiry).ThenBy(x => x.ProductName),
                ("expiry", "desc") => result.OrderByDescending(x => x.EarliestExpiry.HasValue ? 0 : 1).ThenByDescending(x => x.EarliestExpiry).ThenBy(x => x.ProductName),
                ("name", "desc") => result.OrderByDescending(x => x.ProductName),
                _ => result.OrderBy(x => x.ProductName)
            };

            var total = result.Count();

            var pagedItems = result
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return PagedResult<StoreProductInventoryResponse>.Create(
                pagedItems,
                page,
                pageSize,
                total);
        }

        public async Task<PagedResult<IngredientInventoryHistoryResponse>> GetStoreIngredientHistoryAsync(
    int franchiseId,
    InventoryHistoryQuery query,
    CancellationToken ct = default)
        {
            await _access.EnsureCanAccessAsync(franchiseId, ct);

            query ??= new InventoryHistoryQuery();

            var page = query.Page <= 0 ? 1 : query.Page;
            var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
            if (pageSize > 200) pageSize = 200;

            var sortBy = (query.SortBy ?? "createdAt").Trim().ToLowerInvariant();
            var sortDir = (query.SortDir ?? "desc").Trim().ToLowerInvariant();
            if (sortDir is not ("asc" or "desc"))
                throw new ArgumentException("sortDir must be asc or desc.");

            var type = (query.Type ?? "ALL").Trim().ToUpperInvariant();

            IQueryable<InventoryMovement> q = _db.InventoryMovements
                .AsNoTracking()
                .Include(x => x.Batch)
                    .ThenInclude(b => b.Ingredient)
                .Where(x =>
                    x.Batch.FranchiseId == franchiseId &&
                    x.Batch.Type == InventoryOwnerType.Franchise);

            if (type != "ALL")
                q = q.Where(x => x.Type == type);

            if (query.IngredientId.HasValue && query.IngredientId.Value > 0)
                q = q.Where(x => x.Batch.IngredientId == query.IngredientId.Value);

            if (query.FromUtc.HasValue)
            {
                var from = DateTime.SpecifyKind(query.FromUtc.Value, DateTimeKind.Utc);
                q = q.Where(x => x.CreatedAt >= from);
            }

            if (query.ToUtc.HasValue)
            {
                var to = DateTime.SpecifyKind(query.ToUtc.Value, DateTimeKind.Utc);
                q = q.Where(x => x.CreatedAt <= to);
            }

            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var term = query.Q.Trim();
                q = q.Where(x =>
                    EF.Functions.ILike(x.Batch.Ingredient.Name, $"%{term}%") ||
                    EF.Functions.ILike(x.Batch.BatchCode, $"%{term}%") ||
                    (x.Reason != null && EF.Functions.ILike(x.Reason, $"%{term}%")));
            }

            q = ApplyIngredientHistorySort(q, sortBy, sortDir);

            var total = await q.CountAsync(ct);

            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new IngredientInventoryHistoryResponse
                {
                    MovementId = x.MovementId,
                    BatchId = x.BatchId,
                    IngredientId = x.Batch.IngredientId,
                    IngredientName = x.Batch.Ingredient.Name,
                    Unit = x.Batch.Ingredient.Unit,
                    BatchCode = x.Batch.BatchCode,
                    ExpiredAt = x.Batch.ExpiredAt,
                    Type = x.Type,
                    Quantity = x.Quantity,
                    DeliveryId = x.DeliveryId,
                    CreatedByUserId = x.CreatedByUserId,
                    Reason = x.Reason,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(ct);

            return PagedResult<IngredientInventoryHistoryResponse>.Create(
                items,
                page,
                pageSize,
                total);
        }

        public async Task<PagedResult<ProductInventoryHistoryResponse>> GetStoreProductHistoryAsync(
    int franchiseId,
    InventoryHistoryQuery query,
    CancellationToken ct = default)
        {
            await _access.EnsureCanAccessAsync(franchiseId, ct);

            query ??= new InventoryHistoryQuery();

            var page = query.Page <= 0 ? 1 : query.Page;
            var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
            if (pageSize > 200) pageSize = 200;

            var sortBy = (query.SortBy ?? "createdAt").Trim().ToLowerInvariant();
            var sortDir = (query.SortDir ?? "desc").Trim().ToLowerInvariant();
            if (sortDir is not ("asc" or "desc"))
                throw new ArgumentException("sortDir must be asc or desc.");

            var type = (query.Type ?? "ALL").Trim().ToUpperInvariant();

            IQueryable<ProductMovement> q = _db.ProductMovements
                .AsNoTracking()
                .Include(x => x.Batch)
                    .ThenInclude(b => b.Product)
                .Where(x => x.Batch.FranchiseId == franchiseId);

            if (type != "ALL")
                q = q.Where(x => x.Type == type);

            if (query.ProductId.HasValue && query.ProductId.Value > 0)
                q = q.Where(x => x.Batch.ProductId == query.ProductId.Value);

            if (query.FromUtc.HasValue)
            {
                var from = DateTime.SpecifyKind(query.FromUtc.Value, DateTimeKind.Utc);
                q = q.Where(x => x.CreatedAt >= from);
            }

            if (query.ToUtc.HasValue)
            {
                var to = DateTime.SpecifyKind(query.ToUtc.Value, DateTimeKind.Utc);
                q = q.Where(x => x.CreatedAt <= to);
            }

            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var term = query.Q.Trim();
                q = q.Where(x =>
                    EF.Functions.ILike(x.Batch.Product.Name, $"%{term}%") ||
                    EF.Functions.ILike(x.Batch.Product.Sku, $"%{term}%") ||
                    EF.Functions.ILike(x.Batch.BatchCode, $"%{term}%") ||
                    (x.Reason != null && EF.Functions.ILike(x.Reason, $"%{term}%")));
            }

            q = ApplyProductHistorySort(q, sortBy, sortDir);

            var total = await q.CountAsync(ct);

            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ProductInventoryHistoryResponse
                {
                    MovementId = x.MovementId,
                    BatchId = x.BatchId,
                    ProductId = x.Batch.ProductId,
                    ProductName = x.Batch.Product.Name,
                    Sku = x.Batch.Product.Sku,
                    Unit = x.Batch.Product.Unit,
                    ProductType = x.Batch.Product.ProductType,
                    BatchCode = x.Batch.BatchCode,
                    ExpiredAt = x.Batch.ExpiredAt,
                    Type = x.Type,
                    Quantity = x.Quantity,
                    DeliveryId = x.DeliveryId,
                    CreatedByUserId = x.CreatedByUserId,
                    Reason = x.Reason,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(ct);

            return PagedResult<ProductInventoryHistoryResponse>.Create(
                items,
                page,
                pageSize,
                total);
        }

        //HELPERS
        private async Task<int> GetIntSettingAsync(string key, int fallback, CancellationToken ct)
        {
            var raw = await _db.SystemSettings
                .AsNoTracking()
                .Where(x => x.Key == key)
                .Select(x => x.Value)
                .FirstOrDefaultAsync(ct);

            if (int.TryParse(raw, out var value) && value > 0)
                return value;

            return fallback;
        }

        private static IQueryable<InventoryMovement> ApplyIngredientHistorySort(
    IQueryable<InventoryMovement> q,
    string sortBy,
    string sortDir)
        {
            var desc = sortDir == "desc";

            return sortBy switch
            {
                "quantity" => desc
                    ? q.OrderByDescending(x => x.Quantity).ThenByDescending(x => x.MovementId)
                    : q.OrderBy(x => x.Quantity).ThenBy(x => x.MovementId),

                "type" => desc
                    ? q.OrderByDescending(x => x.Type).ThenByDescending(x => x.MovementId)
                    : q.OrderBy(x => x.Type).ThenBy(x => x.MovementId),

                "createdat" or _ => desc
                    ? q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.MovementId)
                    : q.OrderBy(x => x.CreatedAt).ThenBy(x => x.MovementId),
            };
        }

        private static IQueryable<ProductMovement> ApplyProductHistorySort(
            IQueryable<ProductMovement> q,
            string sortBy,
            string sortDir)
        {
            var desc = sortDir == "desc";

            return sortBy switch
            {
                "quantity" => desc
                    ? q.OrderByDescending(x => x.Quantity).ThenByDescending(x => x.MovementId)
                    : q.OrderBy(x => x.Quantity).ThenBy(x => x.MovementId),

                "type" => desc
                    ? q.OrderByDescending(x => x.Type).ThenByDescending(x => x.MovementId)
                    : q.OrderBy(x => x.Type).ThenBy(x => x.MovementId),

                "createdat" or _ => desc
                    ? q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.MovementId)
                    : q.OrderBy(x => x.CreatedAt).ThenBy(x => x.MovementId),
            };
        }

        public async Task<IngredientWasteResponse> CreateIngredientWasteAsync(
            int franchiseId,
            CreateIngredientWasteDto request,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessAsync(franchiseId, ct);

            if (request == null)
                throw new ArgumentException("Request body is required.");

            if (request.BatchId <= 0)
                throw new ArgumentException("BatchId must be positive.");

            if (request.Quantity <= 0)
                throw new ArgumentException("Quantity must be > 0.");

            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new ArgumentException("Reason is required.");

            var batch = await _db.IngredientBatches
                .FirstOrDefaultAsync(b =>
                    b.BatchId == request.BatchId &&
                    b.Type == InventoryOwnerType.Franchise &&
                    b.FranchiseId == franchiseId,
                    ct);

            if (batch is null)
                throw new KeyNotFoundException($"IngredientBatch {request.BatchId} not found.");

            var before = batch.Quantity;
            if (before < request.Quantity)
                throw new InvalidOperationException("Waste quantity exceeds available stock.");

            var after = before - request.Quantity;
            var now = DateTime.UtcNow;
            var reason = BuildReason(request.Reason, request.Reference);

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            batch.Quantity = after;
            await _db.SaveChangesAsync(ct);

            var mv = new InventoryMovement
            {
                BatchId = batch.BatchId,
                Type = "WASTE",
                Quantity = request.Quantity,
                CreatedByUserId = _current.UserId,
                Reason = reason,
                CreatedAt = now
            };

            _db.InventoryMovements.Add(mv);
            await _db.SaveChangesAsync(ct);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = _current.UserId,
                FranchiseId = franchiseId,
                Action = "INGREDIENT_WASTE",
                EntityName = "IngredientBatch",
                EntityId = batch.BatchId,
                OldDataJson = JsonSerializer.Serialize(new
                {
                    batch.BatchId,
                    batch.IngredientId,
                    batch.BatchCode,
                    batch.ExpiredAt,
                    Quantity = before
                }),
                NewDataJson = JsonSerializer.Serialize(new
                {
                    batch.BatchId,
                    batch.IngredientId,
                    batch.BatchCode,
                    batch.ExpiredAt,
                    Quantity = after,
                    Movement = new
                    {
                        mv.MovementId,
                        mv.Type,
                        WasteQuantity = request.Quantity,
                        mv.CreatedAt,
                        mv.Reason
                    }
                }),
                Reason = reason,
                CreatedAt = now
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new IngredientWasteResponse
            {
                BatchId = batch.BatchId,
                MovementId = mv.MovementId,
                FranchiseId = franchiseId,
                IngredientId = batch.IngredientId,
                BatchCode = batch.BatchCode,
                ExpiredAt = batch.ExpiredAt,
                BeforeQuantity = before,
                WasteQuantity = request.Quantity,
                AfterQuantity = after,
                Reason = reason,
                CreatedAt = now
            };
        }
    }
}