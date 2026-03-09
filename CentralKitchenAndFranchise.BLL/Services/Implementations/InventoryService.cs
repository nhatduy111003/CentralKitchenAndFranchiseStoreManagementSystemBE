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

        // BR-18: bắt buộc batchCode + expiredAt + qty>0
        // BR-21: unique batchCode theo (ingredientId, batchCode, franchiseId) đã có unique index
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

            // DateOnly default = 0001-01-01 => coi như thiếu
            if (request.ExpiredAt == default)
                throw new ArgumentException("ExpiredAt is required.");

            if (request.Quantity <= 0)
                throw new ArgumentException("Quantity must be > 0.");

            // ensure ingredient exists & active (tuỳ bạn có Status)
            var ingredient = await _db.Ingredients.AsNoTracking()
                .FirstOrDefaultAsync(x => x.IngredientId == request.IngredientId, ct);

            if (ingredient is null)
                throw new KeyNotFoundException($"Ingredient {request.IngredientId} not found.");

            var now = DateTime.UtcNow;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // create batch
            var batch = new IngredientBatch
            {
                IngredientId = request.IngredientId,
                Type = InventoryOwnerType.Franchise,
                FranchiseId = franchiseId,
                CentralKitchenId = null,
                BatchCode = request.BatchCode.Trim(),
                ExpiredAt = request.ExpiredAt,
                Quantity = request.Quantity
            };

            _db.IngredientBatches.Add(batch);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // unique index hit => BR-21
                throw new InvalidOperationException("BatchCode already exists for this ingredient in this franchise.");
            }

            // create movement IN (ngày nhập = movement.CreatedAt)
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

            // audit log (không bắt buộc trong BR-18, nhưng rất nên để truy vết)
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
                    batch.ExpiredAt,
                    batch.Quantity,
                    Movement = new { mv.MovementId, mv.Type, mv.Quantity, mv.CreatedAt, mv.Reason }
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
                ExpiredAt = batch.ExpiredAt ?? request.ExpiredAt, // safety
                Quantity = batch.Quantity,
                CreatedMovementId = mv.MovementId,
                CreatedAt = mv.CreatedAt
            };
        }

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
                         p => p.ProductionPlanId == productionPlanId
                           && p.CentralKitchenId == centralKitchenId,
                         ct);
            if (plan is null)
                throw new KeyNotFoundException($"ProductionPlan {productionPlanId} not found.");

            if (plan.Items.Count == 0)
                throw new InvalidOperationException("Production plan has no items.");

            // 1) Get BOM for each product (latest ACTIVE by Version)
            var productIds = plan.Items.Select(i => i.ProductId).Distinct().ToList();

            var boms = await _db.Boms.AsNoTracking()
                .Where(b => productIds.Contains(b.ProductId) && b.Status == "ACTIVE")
                .ToListAsync(ct);

            // group by product -> pick max version
            var bomByProduct = boms
                .GroupBy(b => b.ProductId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Version).First());

            var missingBomProducts = productIds.Where(pid => !bomByProduct.ContainsKey(pid)).ToList();
            if (missingBomProducts.Count > 0)
                throw new InvalidOperationException($"Missing ACTIVE BOM for products: {string.Join(",", missingBomProducts)}");

            var bomIds = bomByProduct.Values.Select(b => b.BomId).Distinct().ToList();

            var bomItems = await _db.BomItems.AsNoTracking()
                .Where(x => bomIds.Contains(x.BomId))
                .ToListAsync(ct);

            // 2) Compute required ingredients: sum(planQty * bomItemQty)
            // map bomId -> items
            var bomItemMap = bomItems.GroupBy(x => x.BomId).ToDictionary(g => g.Key, g => g.ToList());

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

            // preload ingredient names (for response)
            var ingIds = required.Keys.ToList();
            var ingNameMap = await _db.Ingredients.AsNoTracking()
                .Where(i => ingIds.Contains(i.IngredientId))
                .Select(i => new { i.IngredientId, i.Name })
                .ToDictionaryAsync(x => x.IngredientId, x => x.Name, ct);

            var now = DateTime.UtcNow;
            var reason = string.IsNullOrWhiteSpace(request.Reason)
                ? $"Issue ingredients for ProductionPlan {productionPlanId}"
                : request.Reason.Trim();

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // 3) For each ingredient: check total available + FEFO pick
            var response = new IssueIngredientsByProductionPlanResponse
            {
                ProductionPlanId = productionPlanId,
                CentralKitchenId = centralKitchenId,
                PlanDate = plan.PlanDate,
                IssuedAt = now
            };

            foreach (var (ingredientId, requiredQty) in required.OrderBy(x => x.Key))
            {
                // load batches with tracking for update
                var batches = await _db.IngredientBatches
                            .Where(b =>
                                b.Type == InventoryOwnerType.CentralKitchen &&
                                b.CentralKitchenId == centralKitchenId &&
                                b.IngredientId == ingredientId &&
                                b.Quantity > 0)
                            .OrderBy(b => b.ExpiredAt == null)
                            .ThenBy(b => b.ExpiredAt)
                            .ThenBy(b => b.BatchId)
                            .ToListAsync(ct);

                var available = batches.Sum(b => b.Quantity);
                if (available < requiredQty)
                    throw new InvalidOperationException($"Insufficient inventory for IngredientId={ingredientId}. Required={requiredQty}, Available={available}.");

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

                    // deduct
                    batch.Quantity -= take;

                    // movement OUT
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
                    await _db.SaveChangesAsync(ct); // to get MovementId

                    line.Batches.Add(new IssuedBatchLine
                    {
                        BatchId = batch.BatchId,
                        BatchCode = batch.BatchCode,
                        ExpiredAt = batch.ExpiredAt,
                        IssuedQuantity = take,
                        MovementId = mv.MovementId
                    });

                    remaining -= take;
                }

                response.Lines.Add(line);
            }

            await _db.SaveChangesAsync(ct);

            // 4) Audit log for the whole issue transaction
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
                        Batches = l.Batches.Select(b => new { b.BatchId, b.BatchCode, b.ExpiredAt, b.IssuedQuantity, b.MovementId })
                    })
                }),
                Reason = reason,
                CreatedAt = now
            });

            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            return response;
        }

        public async Task<AdjustIngredientInventoryResponse> AdjustIngredientAsync(
        int franchiseId,
        AdjustIngredientInventoryDto request,
        CancellationToken ct = default)
        {
            await _access.EnsureCanAccessAsync(franchiseId, ct);

            if (request.BatchId <= 0)
                throw new ArgumentException("BatchId must be positive.");

            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new ArgumentException("Reason is required."); // BR-22

            if (request.DeltaQuantity == 0)
                throw new ArgumentException("DeltaQuantity cannot be 0.");

            var type = (request.Type ?? "ADJUST").Trim().ToUpperInvariant();
            if (type is not ("ADJUST" or "WASTE"))
                throw new ArgumentException("Type must be ADJUST or WASTE.");

            // track batch for update
            var batch = await _db.IngredientBatches
                        .FirstOrDefaultAsync(b =>
                        b.BatchId == request.BatchId &&
                        b.Type == InventoryOwnerType.Franchise &&
                        b.FranchiseId == franchiseId, ct);
            if (batch is null)
                throw new KeyNotFoundException($"IngredientBatch {request.BatchId} not found.");

            var before = batch.Quantity;
            var after = before + request.DeltaQuantity;

            if (after < 0)
                throw new InvalidOperationException("Adjustment would make inventory negative."); // safety

            var now = DateTime.UtcNow;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            batch.Quantity = after;
            await _db.SaveChangesAsync(ct);

            // movement record
            var mv = new InventoryMovement
            {
                BatchId = batch.BatchId,
                Type = type, // ADJUST / WASTE
                Quantity = Math.Abs(request.DeltaQuantity), // store magnitude
                CreatedByUserId = _current.UserId,          // BR-22
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
                ExpiredAt = batch.ExpiredAt,
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

            // ensure product exists
            var product = await _db.Products.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProductId == request.ProductId, ct);

            if (product is null)
                throw new KeyNotFoundException($"Product {request.ProductId} not found.");

            var now = DateTime.UtcNow;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // create product batch
            var batch = new ProductBatch
            {
                ProductId = request.ProductId,
                FranchiseId = franchiseId,
                BatchCode = request.BatchCode.Trim(),
                ExpiredAt = request.ExpiredAt,
                Quantity = request.Quantity,
                CreatedAt = now
            };

            _db.ProductBatches.Add(batch);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // you already have unique index: (ProductId, BatchCode, FranchiseId)
                throw new InvalidOperationException("BatchCode already exists for this product in this franchise.");
            }

            // movement IN
            var mv = new ProductMovement
            {
                BatchId = batch.BatchId,
                Type = "IN",
                Quantity = request.Quantity,
                CreatedByUserId = _current.UserId,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                DeliveryId = null,
                CreatedAt = now
            };

            _db.ProductMovements.Add(mv);
            await _db.SaveChangesAsync(ct);

            // audit log
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
                    batch.ExpiredAt,
                    batch.Quantity,
                    Movement = new { mv.MovementId, mv.Type, mv.Quantity, mv.CreatedAt, mv.Reason }
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
                ExpiredAt = batch.ExpiredAt,
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
    }
}

