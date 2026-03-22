using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Constants;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Extensions;
using CentralKitchenAndFranchise.DTO.Requests.Ingredients;
using CentralKitchenAndFranchise.DTO.Responses.Inventory;
using CentralKitchenAndFranchise.DTO.Requests.Inventory;

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

        // INGREDIENT INBOUND
        // - ExpiredAt được derive từ CreatedAt + ShelfLifeDays
        //Inbound cho Franchise 
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


        // ISSUE INGREDIENTS FOR PRODUCTION PLAN
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

        // ADJUST / WASTE INGREDIENT INVENTORY
        // - Chỉ adjust batch ingredient thuộc franchise hiện tại
        // - Phải Include Ingredient vì response/audit cần ExpiredAt derived

        //Adjust cho Franchise
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
                CentralKitchenId = null,
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

        //Adjust cho Ck
        public async Task<AdjustIngredientInventoryResponse> AdjustCentralKitchenIngredientAsync(
            int centralKitchenId,
            AdjustIngredientInventoryDto request,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

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
                    b.Type == InventoryOwnerType.CentralKitchen &&
                    b.CentralKitchenId == centralKitchenId,
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

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = _current.UserId,
                CentralKitchenId = centralKitchenId,
                Action = type == "WASTE" ? "CK_INGREDIENT_WASTE" : "CK_INGREDIENT_ADJUST",
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
                FranchiseId = null,
                CentralKitchenId = centralKitchenId,
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

        private static DateTime ResolveManualBatchCreatedAt(DateTime? createdAtUtc, DateTime fallbackUtc)
        {
            var value = createdAtUtc?.ToUniversalTime() ?? fallbackUtc;

            if (value > fallbackUtc.AddMinutes(5))
                throw new ArgumentException("CreatedAtUtc cannot be in the future.");

            return value;
        }

        private static CentralKitchenIngredientBatchResponse MapCentralKitchenIngredientBatch(IngredientBatch batch)
        {
            return new CentralKitchenIngredientBatchResponse
            {
                BatchId = batch.BatchId,
                CentralKitchenId = batch.CentralKitchenId!.Value,
                IngredientId = batch.IngredientId,
                IngredientName = batch.Ingredient.Name,
                Unit = batch.Ingredient.Unit,
                BatchCode = batch.BatchCode,
                Quantity = batch.Quantity,
                CreatedAt = batch.CreatedAt,
                ExpiredAt = batch.CalculateExpiredAt()
            };
        }

        private static CentralKitchenProductBatchResponse MapCentralKitchenProductBatch(ProductBatch batch)
        {
            return new CentralKitchenProductBatchResponse
            {
                BatchId = batch.BatchId,
                CentralKitchenId = batch.CentralKitchenId!.Value,
                ProductId = batch.ProductId,
                ProductName = batch.Product.Name,
                Unit = batch.Product.Unit,
                BatchCode = batch.BatchCode,
                Quantity = batch.Quantity,
                CreatedAt = batch.CreatedAt,
                ExpiredAt = batch.CalculateExpiredAt()
            };
        }

        // PRODUCT INBOUND
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

        public async Task<FranchiseInventorySummaryResponse> GetFranchiseInventorySummaryAsync(
             int franchiseId,
             CancellationToken ct = default)
        {
            await _access.EnsureCanAccessAsync(franchiseId, ct);

            var ingredientBatches = await _db.IngredientBatches
                .AsNoTracking()
                .Include(x => x.Ingredient)
                .Where(x => x.FranchiseId == franchiseId && x.Quantity > 0)
                .ToListAsync(ct);

            ingredientBatches = ingredientBatches
                .OrderBy(x => x.CalculateExpiredAt() == null)
                .ThenBy(x => x.CalculateExpiredAt())
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.BatchId)
                .ToList();

            var productBatches = await _db.ProductBatches
                .AsNoTracking()
                .Include(x => x.Product)
                .Where(x => x.FranchiseId == franchiseId && x.Quantity > 0)
                .ToListAsync(ct);

            productBatches = productBatches
                .OrderBy(x => x.CalculateExpiredAt() == null)
                .ThenBy(x => x.CalculateExpiredAt())
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.BatchId)
                .ToList();

            var ingredientItems = ingredientBatches
                .GroupBy(x => new { x.IngredientId, x.Ingredient.Name, x.Ingredient.Unit, x.Ingredient.SafetyStock })
                .Select(g => new FranchiseInventorySummaryItemResponse
                {
                    ItemType = "INGREDIENT",
                    ItemId = g.Key.IngredientId,
                    ItemName = g.Key.Name,
                    Unit = g.Key.Unit,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    LowStockThreshold = g.Key.SafetyStock,
                    IsLowStock = g.Key.SafetyStock > 0 && g.Sum(x => x.Quantity) < g.Key.SafetyStock,
                    Batches = g.Select(x => new FranchiseInventoryBatchResponse
                    {
                        BatchId = x.BatchId,
                        BatchCode = x.BatchCode,
                        ExpiredAt = x.CalculateExpiredAt(),
                        Quantity = x.Quantity
                    }).ToList()
                });

            var productItems = productBatches
                .GroupBy(x => new { x.ProductId, x.Product.Name, x.Product.Unit })
                .Select(g => new FranchiseInventorySummaryItemResponse
                {
                    ItemType = "PRODUCT",
                    ItemId = g.Key.ProductId,
                    ItemName = g.Key.Name,
                    Unit = g.Key.Unit,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    LowStockThreshold = null,
                    IsLowStock = false,
                    Batches = g.Select(x => new FranchiseInventoryBatchResponse
                    {
                        BatchId = x.BatchId,
                        BatchCode = x.BatchCode,
                        ExpiredAt = x.CalculateExpiredAt(),
                        Quantity = x.Quantity
                    }).ToList()
                });

            return new FranchiseInventorySummaryResponse
            {
                Items = ingredientItems
                    .Concat(productItems)
                    .OrderBy(x => x.ItemType)
                    .ThenBy(x => x.ItemName)
                    .ToList()
            };
        }

        public async Task<CentralKitchenInventorySummaryResponse> GetCentralKitchenInventorySummaryAsync(
            int centralKitchenId,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

            var ingredientBatches = await _db.IngredientBatches
                .AsNoTracking()
                .Include(x => x.Ingredient)
                .Where(x =>
                    x.Type == InventoryOwnerType.CentralKitchen &&
                    x.CentralKitchenId == centralKitchenId &&
                    x.FranchiseId == null &&
                    x.Quantity > 0)
                .ToListAsync(ct);

            ingredientBatches = ingredientBatches
                .OrderBy(x => x.CalculateExpiredAt() == null)
                .ThenBy(x => x.CalculateExpiredAt())
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.BatchId)
                .ToList();

            var productBatches = await _db.ProductBatches
                .AsNoTracking()
                .Include(x => x.Product)
                .Where(x =>
                    x.CentralKitchenId == centralKitchenId &&
                    x.FranchiseId == null &&
                    x.Quantity > 0)
                .ToListAsync(ct);

            productBatches = productBatches
                .OrderBy(x => x.CalculateExpiredAt() == null)
                .ThenBy(x => x.CalculateExpiredAt())
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.BatchId)
                .ToList();

            var ingredientItems = ingredientBatches
                .GroupBy(x => new { x.IngredientId, x.Ingredient.Name, x.Ingredient.Unit, x.Ingredient.SafetyStock })
                .Select(g => new CentralKitchenInventorySummaryItemResponse
                {
                    ItemType = "INGREDIENT",
                    ItemId = g.Key.IngredientId,
                    ItemName = g.Key.Name,
                    Unit = g.Key.Unit,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    LowStockThreshold = g.Key.SafetyStock,
                    IsLowStock = g.Key.SafetyStock > 0 && g.Sum(x => x.Quantity) < g.Key.SafetyStock,
                    Batches = g.Select(x => new CentralKitchenInventoryBatchResponse
                    {
                        BatchId = x.BatchId,
                        BatchCode = x.BatchCode,
                        ExpiredAt = x.CalculateExpiredAt(),
                        Quantity = x.Quantity
                    }).ToList()
                });

            var productItems = productBatches
                .GroupBy(x => new { x.ProductId, x.Product.Name, x.Product.Unit })
                .Select(g => new CentralKitchenInventorySummaryItemResponse
                {
                    ItemType = "PRODUCT",
                    ItemId = g.Key.ProductId,
                    ItemName = g.Key.Name,
                    Unit = g.Key.Unit,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    LowStockThreshold = null,
                    IsLowStock = false,
                    Batches = g.Select(x => new CentralKitchenInventoryBatchResponse
                    {
                        BatchId = x.BatchId,
                        BatchCode = x.BatchCode,
                        ExpiredAt = x.CalculateExpiredAt(),
                        Quantity = x.Quantity
                    }).ToList()
                });

            return new CentralKitchenInventorySummaryResponse
            {
                Items = ingredientItems
                    .Concat(productItems)
                    .OrderBy(x => x.ItemType)
                    .ThenBy(x => x.ItemName)
                    .ToList()
            };
        }

        // CRUD ingredient batches

        public async Task<CentralKitchenIngredientBatchResponse> InboundCentralKitchenIngredientAsync(
            int centralKitchenId,
            CreateIngredientBatchDto request,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

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

            if (!string.Equals(ingredient.Status, IngredientStatus.Active, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Ingredient {request.IngredientId} is not ACTIVE.");

            var now = DateTime.UtcNow;
            var createdAt = ResolveManualBatchCreatedAt(request.CreatedAtUtc, now);

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var batch = new IngredientBatch
            {
                IngredientId = request.IngredientId,
                Type = InventoryOwnerType.CentralKitchen,
                FranchiseId = null,
                CentralKitchenId = centralKitchenId,
                BatchCode = request.BatchCode.Trim(),
                Quantity = request.Quantity,
                CreatedAt = createdAt
            };

            batch.Ingredient = ingredient;

            _db.IngredientBatches.Add(batch);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                throw new InvalidOperationException("BatchCode already exists for this ingredient in this central kitchen.");
            }

            var mv = new InventoryMovement
            {
                BatchId = batch.BatchId,
                Type = InventoryMovementType.In,
                Quantity = request.Quantity,
                CreatedByUserId = _current.UserId,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Manual central kitchen ingredient batch create" : request.Reason.Trim(),
                CreatedAt = now
            };

            _db.InventoryMovements.Add(mv);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = _current.UserId,
                CentralKitchenId = centralKitchenId,
                Action = "CK_INGREDIENT_BATCH_CREATE",
                EntityName = "IngredientBatch",
                EntityId = batch.BatchId,
                OldDataJson = null,
                NewDataJson = JsonSerializer.Serialize(new
                {
                    batch.BatchId,
                    batch.IngredientId,
                    batch.CentralKitchenId,
                    batch.BatchCode,
                    batch.Quantity,
                    batch.CreatedAt,
                    ExpiredAt = batch.CalculateExpiredAt()
                }),
                Reason = mv.Reason,
                CreatedAt = now
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return MapCentralKitchenIngredientBatch(batch);
        }

        public async Task<List<CentralKitchenIngredientBatchResponse>> GetCentralKitchenIngredientBatchesAsync(
            int centralKitchenId,
            int? ingredientId = null,
            bool includeZero = false,
            CancellationToken ct = default)
            {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

            var query = _db.IngredientBatches
                .AsNoTracking()
                .Include(x => x.Ingredient)
                .Where(x =>
                    x.Type == InventoryOwnerType.CentralKitchen &&
                    x.CentralKitchenId == centralKitchenId);

            if (ingredientId.HasValue)
                query = query.Where(x => x.IngredientId == ingredientId.Value);

            if (!includeZero)
                query = query.Where(x => x.Quantity > 0);

            var batches = await query.ToListAsync(ct);

            batches = batches
                .OrderBy(x => x.CalculateExpiredAt() == null)
                .ThenBy(x => x.CalculateExpiredAt())
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.BatchId)
                .ToList();

            return batches.Select(MapCentralKitchenIngredientBatch).ToList();
        }

        public async Task<CentralKitchenIngredientBatchResponse> GetCentralKitchenIngredientBatchByIdAsync(
            int centralKitchenId,
            int batchId,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

            var batch = await _db.IngredientBatches
                .AsNoTracking()
                .Include(x => x.Ingredient)
                .FirstOrDefaultAsync(x =>
                    x.BatchId == batchId &&
                    x.Type == InventoryOwnerType.CentralKitchen &&
                    x.CentralKitchenId == centralKitchenId,
                    ct);

            if (batch is null)
                throw new KeyNotFoundException($"IngredientBatch {batchId} not found.");

            return MapCentralKitchenIngredientBatch(batch);
        }

        public async Task<CentralKitchenIngredientBatchResponse> UpdateCentralKitchenIngredientBatchCodeAsync(
            int centralKitchenId,
            int batchId,
            UpdateBatchCodeRequest request,
            CancellationToken ct = default)
            {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

            if (string.IsNullOrWhiteSpace(request.BatchCode))
                throw new ArgumentException("BatchCode is required.");

            var batch = await _db.IngredientBatches
                .Include(x => x.Ingredient)
                .FirstOrDefaultAsync(x =>
                    x.BatchId == batchId &&
                    x.Type == InventoryOwnerType.CentralKitchen &&
                    x.CentralKitchenId == centralKitchenId,
                    ct);

            if (batch is null)
                throw new KeyNotFoundException($"IngredientBatch {batchId} not found.");

            var movementCount = await _db.InventoryMovements
                .CountAsync(x => x.BatchId == batchId, ct);

            if (movementCount > 1)
                throw new InvalidOperationException("BatchCode can only be changed before any follow-up movements happen.");

            var oldCode = batch.BatchCode;
            batch.BatchCode = request.BatchCode.Trim();

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                throw new InvalidOperationException("BatchCode already exists for this ingredient in this central kitchen.");
            }

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = _current.UserId,
                CentralKitchenId = centralKitchenId,
                Action = "CK_INGREDIENT_BATCH_RENAME",
                EntityName = "IngredientBatch",
                EntityId = batch.BatchId,
                OldDataJson = JsonSerializer.Serialize(new { BatchCode = oldCode }),
                NewDataJson = JsonSerializer.Serialize(new { BatchCode = batch.BatchCode }),
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Manual batch code update" : request.Reason.Trim(),
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);

            return MapCentralKitchenIngredientBatch(batch);
        }

        public async Task DeleteCentralKitchenIngredientBatchAsync(
            int centralKitchenId,
            int batchId,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

            var batch = await _db.IngredientBatches
                .Include(x => x.Ingredient)
                .FirstOrDefaultAsync(x =>
                    x.BatchId == batchId &&
                    x.Type == InventoryOwnerType.CentralKitchen &&
                    x.CentralKitchenId == centralKitchenId,
                    ct);

            if (batch is null)
                throw new KeyNotFoundException($"IngredientBatch {batchId} not found.");

            if (batch.Quantity != 0)
                throw new InvalidOperationException("Only zero-quantity batches can be deleted.");

            var movements = await _db.InventoryMovements
                .Where(x => x.BatchId == batchId)
                .ToListAsync(ct);

            if (movements.Any(x => x.DeliveryId != null || x.Type == InventoryMovementType.Out))
                throw new InvalidOperationException("Cannot delete a batch that has been used in transfer/delivery.");

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            _db.InventoryMovements.RemoveRange(movements);
            _db.IngredientBatches.Remove(batch);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = _current.UserId,
                CentralKitchenId = centralKitchenId,
                Action = "CK_INGREDIENT_BATCH_DELETE",
                EntityName = "IngredientBatch",
                EntityId = batchId,
                OldDataJson = JsonSerializer.Serialize(new
                {
                    batch.BatchId,
                    batch.IngredientId,
                    batch.BatchCode,
                    batch.Quantity,
                    batch.CreatedAt,
                    ExpiredAt = batch.CalculateExpiredAt()
                }),
                NewDataJson = null,
                Reason = "Manual central kitchen ingredient batch delete",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        // CRUD product batches
        public async Task<CentralKitchenProductBatchResponse> InboundCentralKitchenProductAsync(
            int centralKitchenId,
            CreateCentralKitchenProductBatchDto request,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

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
            var createdAt = ResolveManualBatchCreatedAt(request.CreatedAtUtc, now);

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var batch = new ProductBatch
            {
                ProductId = request.ProductId,
                FranchiseId = null,
                CentralKitchenId = centralKitchenId,
                BatchCode = request.BatchCode.Trim(),
                Quantity = request.Quantity,
                CreatedAt = createdAt,
                ProductionRunId = null
            };

            batch.Product = product;

            _db.ProductBatches.Add(batch);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                throw new InvalidOperationException("BatchCode already exists for this product in this central kitchen.");
            }

            var mv = new ProductMovement
            {
                BatchId = batch.BatchId,
                Type = MovementType.In,
                Quantity = request.Quantity,
                CreatedByUserId = _current.UserId,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Manual central kitchen product batch create" : request.Reason.Trim(),
                DeliveryId = null,
                CreatedAt = now
            };

            _db.ProductMovements.Add(mv);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = _current.UserId,
                CentralKitchenId = centralKitchenId,
                Action = "CK_PRODUCT_BATCH_CREATE",
                EntityName = "ProductBatch",
                EntityId = batch.BatchId,
                OldDataJson = null,
                NewDataJson = JsonSerializer.Serialize(new
                {
                    batch.BatchId,
                    batch.ProductId,
                    batch.CentralKitchenId,
                    batch.BatchCode,
                    batch.Quantity,
                    batch.CreatedAt,
                    ExpiredAt = batch.CalculateExpiredAt()
                }),
                Reason = mv.Reason,
                CreatedAt = now
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return MapCentralKitchenProductBatch(batch);
        }

        public async Task<AdjustProductInventoryResponse> AdjustCentralKitchenProductAsync(
            int centralKitchenId,
            AdjustProductInventoryDto request,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

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

            var batch = await _db.ProductBatches
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b =>
                    b.BatchId == request.BatchId &&
                    b.CentralKitchenId == centralKitchenId &&
                    b.FranchiseId == null,
                    ct);

            if (batch is null)
                throw new KeyNotFoundException($"ProductBatch {request.BatchId} not found.");

            var before = batch.Quantity;
            var after = before + request.DeltaQuantity;

            if (after < 0)
                throw new InvalidOperationException("Adjustment would make inventory negative.");

            var now = DateTime.UtcNow;
            var expiredAt = batch.CalculateExpiredAt();

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            batch.Quantity = after;
            await _db.SaveChangesAsync(ct);

            var mv = new ProductMovement
            {
                BatchId = batch.BatchId,
                Type = type,
                Quantity = Math.Abs(request.DeltaQuantity),
                CreatedByUserId = _current.UserId,
                Reason = BuildReason(request.Reason, request.Reference),
                DeliveryId = null,
                CreatedAt = now
            };

            _db.ProductMovements.Add(mv);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = _current.UserId,
                CentralKitchenId = centralKitchenId,
                Action = type == "WASTE" ? "CK_PRODUCT_WASTE" : "CK_PRODUCT_ADJUST",
                EntityName = "ProductBatch",
                EntityId = batch.BatchId,
                OldDataJson = JsonSerializer.Serialize(new
                {
                    batch.BatchId,
                    batch.ProductId,
                    batch.BatchCode,
                    batch.CreatedAt,
                    ExpiredAt = expiredAt,
                    Quantity = before
                }),
                NewDataJson = JsonSerializer.Serialize(new
                {
                    batch.BatchId,
                    batch.ProductId,
                    batch.BatchCode,
                    batch.CreatedAt,
                    ExpiredAt = expiredAt,
                    Quantity = after,
                    Movement = new
                    {
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

            return new AdjustProductInventoryResponse
            {
                BatchId = batch.BatchId,
                MovementId = mv.MovementId,
                CentralKitchenId = centralKitchenId,
                ProductId = batch.ProductId,
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

        public async Task<List<CentralKitchenProductBatchResponse>> GetCentralKitchenProductBatchesAsync(
            int centralKitchenId,
            int? productId = null,
            bool includeZero = false,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

            var query = _db.ProductBatches
                .AsNoTracking()
                .Include(x => x.Product)
                .Where(x =>
                    x.CentralKitchenId == centralKitchenId &&
                    x.FranchiseId == null);

            if (productId.HasValue)
                query = query.Where(x => x.ProductId == productId.Value);

            if (!includeZero)
                query = query.Where(x => x.Quantity > 0);

            var batches = await query.ToListAsync(ct);

            batches = batches
                .OrderBy(x => x.CalculateExpiredAt() == null)
                .ThenBy(x => x.CalculateExpiredAt())
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.BatchId)
                .ToList();

            return batches.Select(MapCentralKitchenProductBatch).ToList();
        }

        public async Task<CentralKitchenProductBatchResponse> GetCentralKitchenProductBatchByIdAsync(
            int centralKitchenId,
            int batchId,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

            var batch = await _db.ProductBatches
                .AsNoTracking()
                .Include(x => x.Product)
                .FirstOrDefaultAsync(x =>
                    x.BatchId == batchId &&
                    x.CentralKitchenId == centralKitchenId &&
                    x.FranchiseId == null,
                    ct);

            if (batch is null)
                throw new KeyNotFoundException($"ProductBatch {batchId} not found.");

            return MapCentralKitchenProductBatch(batch);
        }

        public async Task<CentralKitchenProductBatchResponse> UpdateCentralKitchenProductBatchCodeAsync(
            int centralKitchenId,
            int batchId,
            UpdateBatchCodeRequest request,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

            if (string.IsNullOrWhiteSpace(request.BatchCode))
                throw new ArgumentException("BatchCode is required.");

            var batch = await _db.ProductBatches
                .Include(x => x.Product)
                .FirstOrDefaultAsync(x =>
                    x.BatchId == batchId &&
                    x.CentralKitchenId == centralKitchenId &&
                    x.FranchiseId == null,
                    ct);

            if (batch is null)
                throw new KeyNotFoundException($"ProductBatch {batchId} not found.");

            var movementCount = await _db.ProductMovements
                .CountAsync(x => x.BatchId == batchId, ct);

            if (movementCount > 1)
                throw new InvalidOperationException("BatchCode can only be changed before any follow-up movements happen.");

            var oldCode = batch.BatchCode;
            batch.BatchCode = request.BatchCode.Trim();

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                throw new InvalidOperationException("BatchCode already exists for this product in this central kitchen.");
            }

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = _current.UserId,
                CentralKitchenId = centralKitchenId,
                Action = "CK_PRODUCT_BATCH_RENAME",
                EntityName = "ProductBatch",
                EntityId = batch.BatchId,
                OldDataJson = JsonSerializer.Serialize(new { BatchCode = oldCode }),
                NewDataJson = JsonSerializer.Serialize(new { BatchCode = batch.BatchCode }),
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Manual batch code update" : request.Reason.Trim(),
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);

            return MapCentralKitchenProductBatch(batch);
        }

        public async Task DeleteCentralKitchenProductBatchAsync(
            int centralKitchenId,
            int batchId,
            CancellationToken ct = default)
        {
            await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);

            var batch = await _db.ProductBatches
                .Include(x => x.Product)
                .FirstOrDefaultAsync(x =>
                    x.BatchId == batchId &&
                    x.CentralKitchenId == centralKitchenId &&
                    x.FranchiseId == null,
                    ct);

            if (batch is null)
                throw new KeyNotFoundException($"ProductBatch {batchId} not found.");

            if (batch.Quantity != 0)
                throw new InvalidOperationException("Only zero-quantity batches can be deleted.");

            var movements = await _db.ProductMovements
                .Where(x => x.BatchId == batchId)
                .ToListAsync(ct);

            if (movements.Any(x => x.DeliveryId != null || x.Type == MovementType.Out))
                throw new InvalidOperationException("Cannot delete a batch that has been used in transfer/delivery.");

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            _db.ProductMovements.RemoveRange(movements);
            _db.ProductBatches.Remove(batch);

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = _current.UserId,
                CentralKitchenId = centralKitchenId,
                Action = "CK_PRODUCT_BATCH_DELETE",
                EntityName = "ProductBatch",
                EntityId = batchId,
                OldDataJson = JsonSerializer.Serialize(new
                {
                    batch.BatchId,
                    batch.ProductId,
                    batch.BatchCode,
                    batch.Quantity,
                    batch.CreatedAt,
                    ExpiredAt = batch.CalculateExpiredAt()
                }),
                NewDataJson = null,
                Reason = "Manual central kitchen product batch delete",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
    }
    }