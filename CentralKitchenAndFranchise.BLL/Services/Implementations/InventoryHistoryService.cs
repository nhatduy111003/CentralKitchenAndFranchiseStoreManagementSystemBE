using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Requests.InventoryHistory;
using CentralKitchenAndFranchise.DTO.Responses.Common;
using CentralKitchenAndFranchise.DTO.Responses.InventoryHistory;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public class InventoryHistoryService : IInventoryHistoryService
{
    private static readonly HashSet<string> AllowedItemTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        InventoryHistoryItemTypes.Ingredient,
        InventoryHistoryItemTypes.Product
    };

    private static readonly Dictionary<string, string> EventTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [InventoryLedgerEventTypes.Inbound] = InventoryLedgerEventTypes.Inbound,
        [InventoryLedgerEventTypes.Adjust] = InventoryLedgerEventTypes.Adjust,
        [InventoryLedgerEventTypes.Waste] = InventoryLedgerEventTypes.Waste,
        [InventoryLedgerEventTypes.IssueProd] = InventoryLedgerEventTypes.IssueProd,
        [InventoryLedgerEventTypes.PrepareOut] = InventoryLedgerEventTypes.PrepareOut,
        [InventoryLedgerEventTypes.TransitIn] = InventoryLedgerEventTypes.TransitIn,
        [InventoryLedgerEventTypes.TransitOut] = InventoryLedgerEventTypes.TransitOut,
        [InventoryLedgerEventTypes.ReceiveIn] = InventoryLedgerEventTypes.ReceiveIn,
        [InventoryLedgerEventTypes.Rename] = InventoryLedgerEventTypes.Rename,
        [InventoryLedgerEventTypes.Archive] = InventoryLedgerEventTypes.Archive,
        [InventoryLedgerEventTypes.Reverse] = InventoryLedgerEventTypes.Reverse
    };

    private readonly AppDbContext _db;
    private readonly IFranchiseAccessService _access;

    public InventoryHistoryService(AppDbContext db, IFranchiseAccessService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<PagedResult<InventoryHistoryMovementResponse>> GetFranchiseMovementsAsync(
        int franchiseId,
        InventoryHistoryMovementsQuery query,
        CancellationToken ct = default)
    {
        await _access.EnsureCanAccessAsync(franchiseId, ct);
        return await GetMovementsAsync(InventoryLedgerScopeTypes.Franchise, franchiseId, query, ct);
    }

    public async Task<PagedResult<InventoryHistoryMovementResponse>> GetCentralKitchenMovementsAsync(
        int centralKitchenId,
        InventoryHistoryMovementsQuery query,
        CancellationToken ct = default)
    {
        await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);
        return await GetMovementsAsync(InventoryLedgerScopeTypes.CentralKitchen, centralKitchenId, query, ct);
    }

    public async Task<InventoryBatchLifecycleResponse> GetFranchiseBatchLifecycleAsync(
        int franchiseId,
        int batchId,
        InventoryBatchLifecycleQuery? query,
        CancellationToken ct = default)
    {
        await _access.EnsureCanAccessAsync(franchiseId, ct);
        return await GetBatchLifecycleAsync(InventoryLedgerScopeTypes.Franchise, franchiseId, batchId, query, ct);
    }

    public async Task<InventoryBatchLifecycleResponse> GetCentralKitchenBatchLifecycleAsync(
        int centralKitchenId,
        int batchId,
        InventoryBatchLifecycleQuery? query,
        CancellationToken ct = default)
    {
        await _access.EnsureCanAccessCentralKitchenAsync(centralKitchenId, ct);
        return await GetBatchLifecycleAsync(InventoryLedgerScopeTypes.CentralKitchen, centralKitchenId, batchId, query, ct);
    }

    private async Task<PagedResult<InventoryHistoryMovementResponse>> GetMovementsAsync(
        string scopeType,
        int scopeId,
        InventoryHistoryMovementsQuery query,
        CancellationToken ct)
    {
        var normalized = NormalizeMovementsQuery(query);

        IQueryable<InventoryLedgerEntry> ledgerQuery = _db.InventoryLedgerEntries
            .AsNoTracking()
            .Where(x => x.ScopeType == scopeType && x.ScopeId == scopeId);

        if (normalized.ItemType is not null)
            ledgerQuery = ledgerQuery.Where(x => x.ItemType == normalized.ItemType);

        if (normalized.ItemId.HasValue)
            ledgerQuery = ledgerQuery.Where(x => x.ItemId == normalized.ItemId.Value);

        if (normalized.BatchId.HasValue)
            ledgerQuery = ledgerQuery.Where(x => x.BatchId == normalized.BatchId.Value);

        if (normalized.EventType is not null)
            ledgerQuery = ledgerQuery.Where(x => x.EventType == normalized.EventType);

        if (normalized.DeliveryId.HasValue)
            ledgerQuery = ledgerQuery.Where(x => x.DeliveryId == normalized.DeliveryId.Value);

        if (normalized.FromUtc.HasValue)
            ledgerQuery = ledgerQuery.Where(x => x.OccurredAtUtc >= normalized.FromUtc.Value);

        if (normalized.ToUtc.HasValue)
            ledgerQuery = ledgerQuery.Where(x => x.OccurredAtUtc <= normalized.ToUtc.Value);

        var total = await ledgerQuery.CountAsync(ct);

        ledgerQuery = normalized.SortDir == "asc"
            ? ledgerQuery.OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.InventoryLedgerEntryId)
            : ledgerQuery.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.InventoryLedgerEntryId);

        var rows = await ledgerQuery
            .Skip((normalized.Page - 1) * normalized.PageSize)
            .Take(normalized.PageSize)
            .ToListAsync(ct);

        var enrichment = await BuildEnrichmentAsync(rows, ct);

        return PagedResult<InventoryHistoryMovementResponse>.Create(
            rows.Select(x => MapMovement(x, enrichment)).ToList(),
            normalized.Page,
            normalized.PageSize,
            total);
    }

    private async Task<InventoryBatchLifecycleResponse> GetBatchLifecycleAsync(
        string scopeType,
        int scopeId,
        int batchId,
        InventoryBatchLifecycleQuery? query,
        CancellationToken ct)
    {
        if (batchId <= 0)
            throw new ArgumentException("batchId must be a positive integer.");

        var normalizedItemType = NormalizeOptionalItemType(query?.ItemType);

        var rows = await _db.InventoryLedgerEntries
            .AsNoTracking()
            .Where(x =>
                x.ScopeType == scopeType &&
                x.ScopeId == scopeId &&
                x.BatchId == batchId)
            .ToListAsync(ct);

        if (rows.Count == 0)
            throw new KeyNotFoundException($"No inventory history was found for batch {batchId} in scope {scopeType}:{scopeId}.");

        if (normalizedItemType is not null)
            rows = rows.Where(x => x.ItemType == normalizedItemType).ToList();

        if (rows.Count == 0)
            throw new KeyNotFoundException($"Batch {batchId} exists in scope history but not for itemType {normalizedItemType}.");

        var identities = rows
            .Select(x => new BatchIdentity(x.ItemType, x.ItemId))
            .Distinct()
            .ToList();

        if (identities.Count > 1)
        {
            if (normalizedItemType is null)
            {
                throw new InvalidOperationException(
                    "BatchId is ambiguous across ingredient/product history for this scope. Please provide query param itemType=INGREDIENT or itemType=PRODUCT.");
            }

            throw new InvalidOperationException(
                $"Batch {batchId} resolved to multiple item identities even after filtering by itemType {normalizedItemType}. Data should be reviewed.");
        }

        var identity = identities[0];
        rows = rows
            .Where(x => x.ItemType == identity.ItemType && x.ItemId == identity.ItemId)
            .ToList();

        rows.Sort(BatchLifecycleLedgerComparer.Instance);

        var enrichment = await BuildEnrichmentAsync(rows, ct);
        var currentState = await LoadCurrentBatchStateAsync(scopeType, scopeId, batchId, identity.ItemType, ct);

        var latestRow = rows.Last();
        var firstRowWithBatchSnapshot = rows.FirstOrDefault(x => x.BatchCreatedAtUtc.HasValue || !string.IsNullOrWhiteSpace(x.BatchCodeSnapshot));
        var itemMeta = ResolveItemMeta(identity.ItemType, identity.ItemId, enrichment);

        return new InventoryBatchLifecycleResponse
        {
            BatchId = batchId,
            ItemType = identity.ItemType,
            ItemId = identity.ItemId,
            ItemName = itemMeta.Name,
            ItemUnit = itemMeta.Unit,
            ScopeType = scopeType,
            ScopeId = scopeId,
            ScopeName = ResolveScopeName(scopeType, scopeId, enrichment),
            BatchCode = currentState?.BatchCode
                        ?? latestRow.BatchCodeSnapshot
                        ?? firstRowWithBatchSnapshot?.BatchCodeSnapshot,
            CurrentBatchCode = currentState?.BatchCode,
            BatchCreatedAtUtc = latestRow.BatchCreatedAtUtc ?? firstRowWithBatchSnapshot?.BatchCreatedAtUtc,
            ExpiredAt = latestRow.ExpiredAtSnapshot ?? firstRowWithBatchSnapshot?.ExpiredAtSnapshot,
            CurrentBatchExists = currentState is not null,
            CurrentQuantity = currentState?.Quantity,
            CurrentIsInTransit = currentState?.IsInTransit,
            CurrentBucket = currentState is null
                ? null
                : (currentState.IsInTransit ? InventoryLedgerStockBuckets.Transit : InventoryLedgerStockBuckets.OnHand),
            CurrentDeliveryId = currentState?.DeliveryId,
            CurrentDeliveryCode = currentState?.DeliveryId.HasValue == true
                ? BuildDeliveryCode(currentState.DeliveryId.Value)
                : null,
            Timeline = rows.Select(x => MapMovement(x, enrichment)).ToList()
        };
    }

    private async Task<EnrichmentBundle> BuildEnrichmentAsync(
        IReadOnlyCollection<InventoryLedgerEntry> rows,
        CancellationToken ct)
    {
        var actorUserIds = rows
            .Where(x => x.ActorUserId.HasValue)
            .Select(x => x.ActorUserId!.Value)
            .Distinct()
            .ToList();

        var deliveryIds = rows
            .Where(x => x.DeliveryId.HasValue)
            .Select(x => x.DeliveryId!.Value)
            .Distinct()
            .ToList();

        var storeOrderIds = rows
            .Where(x => x.StoreOrderId.HasValue)
            .Select(x => x.StoreOrderId!.Value)
            .Distinct()
            .ToList();

        var ingredientIds = rows
            .Where(x => x.ItemType == InventoryHistoryItemTypes.Ingredient)
            .Select(x => x.ItemId)
            .Distinct()
            .ToList();

        var productIds = rows
            .Where(x => x.ItemType == InventoryHistoryItemTypes.Product)
            .Select(x => x.ItemId)
            .Distinct()
            .ToList();

        var franchiseIds = rows
            .Where(x => x.ScopeType == InventoryLedgerScopeTypes.Franchise)
            .Select(x => x.ScopeId)
            .Concat(rows.Where(x => x.CounterpartyScopeType == InventoryLedgerScopeTypes.Franchise && x.CounterpartyScopeId.HasValue)
                .Select(x => x.CounterpartyScopeId!.Value))
            .Distinct()
            .ToList();

        var centralKitchenIds = rows
            .Where(x => x.ScopeType == InventoryLedgerScopeTypes.CentralKitchen)
            .Select(x => x.ScopeId)
            .Concat(rows.Where(x => x.CounterpartyScopeType == InventoryLedgerScopeTypes.CentralKitchen && x.CounterpartyScopeId.HasValue)
                .Select(x => x.CounterpartyScopeId!.Value))
            .Distinct()
            .ToList();

        var userMap = actorUserIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Users
                .AsNoTracking()
                .Where(x => actorUserIds.Contains(x.UserId))
                .ToDictionaryAsync(x => x.UserId, x => x.Username, ct);

        var deliveryMap = deliveryIds.Count == 0
            ? new Dictionary<int, DeliveryEnrichment>()
            : await _db.Deliveries
                .AsNoTracking()
                .Where(x => deliveryIds.Contains(x.DeliveryId))
                .Select(x => new DeliveryEnrichment
                {
                    DeliveryId = x.DeliveryId,
                    Status = x.Status
                })
                .ToDictionaryAsync(x => x.DeliveryId, ct);

        var storeOrderMap = storeOrderIds.Count == 0
            ? new Dictionary<int, StoreOrderEnrichment>()
            : await _db.StoreOrders
                .AsNoTracking()
                .Where(x => storeOrderIds.Contains(x.StoreOrderId))
                .Select(x => new StoreOrderEnrichment
                {
                    StoreOrderId = x.StoreOrderId,
                    Status = x.Status
                })
                .ToDictionaryAsync(x => x.StoreOrderId, ct);

        var ingredientMap = ingredientIds.Count == 0
            ? new Dictionary<int, ItemEnrichment>()
            : await _db.Ingredients
                .AsNoTracking()
                .Where(x => ingredientIds.Contains(x.IngredientId))
                .Select(x => new
                {
                    x.IngredientId,
                    x.Name,
                    x.Unit
                })
                .ToDictionaryAsync(x => x.IngredientId, x => new ItemEnrichment(x.Name, x.Unit), ct);

        var productMap = productIds.Count == 0
            ? new Dictionary<int, ItemEnrichment>()
            : await _db.Products
                .AsNoTracking()
                .Where(x => productIds.Contains(x.ProductId))
                .Select(x => new
                {
                    x.ProductId,
                    x.Name,
                    x.Unit
                })
                .ToDictionaryAsync(x => x.ProductId, x => new ItemEnrichment(x.Name, x.Unit), ct);

        var franchiseMap = franchiseIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Franchises
                .AsNoTracking()
                .Where(x => franchiseIds.Contains(x.FranchiseId))
                .ToDictionaryAsync(x => x.FranchiseId, x => x.Name, ct);

        var centralKitchenMap = centralKitchenIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.CentralKitchens
                .AsNoTracking()
                .Where(x => centralKitchenIds.Contains(x.CentralKitchenId))
                .ToDictionaryAsync(x => x.CentralKitchenId, x => x.Name, ct);

        return new EnrichmentBundle(
            Users: userMap,
            Deliveries: deliveryMap,
            StoreOrders: storeOrderMap,
            Ingredients: ingredientMap,
            Products: productMap,
            Franchises: franchiseMap,
            CentralKitchens: centralKitchenMap);
    }

    private async Task<CurrentBatchState?> LoadCurrentBatchStateAsync(
        string scopeType,
        int scopeId,
        int batchId,
        string itemType,
        CancellationToken ct)
    {
        if (itemType == InventoryHistoryItemTypes.Ingredient)
        {
            return await _db.IngredientBatches
                .AsNoTracking()
                .Where(x =>
                    x.BatchId == batchId &&
                    ((scopeType == InventoryLedgerScopeTypes.Franchise && x.FranchiseId == scopeId && x.CentralKitchenId == null) ||
                     (scopeType == InventoryLedgerScopeTypes.CentralKitchen && x.CentralKitchenId == scopeId && x.FranchiseId == null)))
                .Select(x => new CurrentBatchState
                {
                    BatchId = x.BatchId,
                    BatchCode = x.BatchCode,
                    Quantity = x.Quantity,
                    IsInTransit = x.IsInTransit,
                    DeliveryId = x.DeliveryId
                })
                .FirstOrDefaultAsync(ct);
        }

        return await _db.ProductBatches
            .AsNoTracking()
            .Where(x =>
                x.BatchId == batchId &&
                ((scopeType == InventoryLedgerScopeTypes.Franchise && x.FranchiseId == scopeId && x.CentralKitchenId == null) ||
                 (scopeType == InventoryLedgerScopeTypes.CentralKitchen && x.CentralKitchenId == scopeId && x.FranchiseId == null)))
            .Select(x => new CurrentBatchState
            {
                BatchId = x.BatchId,
                BatchCode = x.BatchCode,
                Quantity = x.Quantity,
                IsInTransit = x.IsInTransit,
                DeliveryId = x.DeliveryId
            })
            .FirstOrDefaultAsync(ct);
    }

    private static InventoryHistoryMovementResponse MapMovement(
        InventoryLedgerEntry row,
        EnrichmentBundle enrichment)
    {
        var itemMeta = ResolveItemMeta(row.ItemType, row.ItemId, enrichment);
        var actorDisplay = row.ActorUserId.HasValue && enrichment.Users.TryGetValue(row.ActorUserId.Value, out var username)
            ? username
            : null;

        var deliveryStatus = row.DeliveryId.HasValue && enrichment.Deliveries.TryGetValue(row.DeliveryId.Value, out var delivery)
            ? delivery.Status
            : null;

        var orderStatus = row.StoreOrderId.HasValue && enrichment.StoreOrders.TryGetValue(row.StoreOrderId.Value, out var order)
            ? order.Status
            : null;

        return new InventoryHistoryMovementResponse
        {
            InventoryLedgerEntryId = row.InventoryLedgerEntryId,
            CorrelationId = row.CorrelationId,
            SequenceNo = row.SequenceNo,
            OccurredAtUtc = row.OccurredAtUtc,
            ItemType = row.ItemType,
            ItemId = row.ItemId,
            ItemName = itemMeta.Name,
            ItemUnit = itemMeta.Unit,
            BatchId = row.BatchId,
            BatchCode = row.BatchCodeSnapshot,
            BatchCreatedAtUtc = row.BatchCreatedAtUtc,
            ExpiredAt = row.ExpiredAtSnapshot,
            ScopeType = row.ScopeType,
            ScopeId = row.ScopeId,
            ScopeName = ResolveScopeName(row.ScopeType, row.ScopeId, enrichment),
            StockBucket = row.StockBucket,
            DeltaQuantity = row.DeltaQuantity,
            EventType = row.EventType,
            IsNonStockEvent = row.IsNonStockEvent,
            Reason = row.Reason,
            ActorUserId = row.ActorUserId,
            ActorDisplay = actorDisplay,
            ReferenceType = row.ReferenceType,
            ReferenceId = row.ReferenceId,
            DeliveryId = row.DeliveryId,
            DeliveryCode = row.DeliveryId.HasValue ? BuildDeliveryCode(row.DeliveryId.Value) : null,
            DeliveryStatus = deliveryStatus,
            DeliveryPlanId = row.DeliveryPlanId,
            StoreOrderId = row.StoreOrderId,
            OrderCode = row.StoreOrderId.HasValue ? BuildOrderCode(row.StoreOrderId.Value) : null,
            OrderStatus = orderStatus,
            RequestedQuantitySnapshot = row.RequestedQuantitySnapshot,
            ActualQuantitySnapshot = row.ActualQuantitySnapshot,
            DroppedQuantitySnapshot = row.DroppedQuantitySnapshot,
            DropReasonSnapshot = row.DropReasonSnapshot,
            CounterpartyScopeType = row.CounterpartyScopeType,
            CounterpartyScopeId = row.CounterpartyScopeId,
            CounterpartyScopeName = row.CounterpartyScopeType is not null && row.CounterpartyScopeId.HasValue
                ? ResolveScopeName(row.CounterpartyScopeType, row.CounterpartyScopeId.Value, enrichment)
                : null,
            CounterpartyBatchId = row.CounterpartyBatchId,
            MetadataJson = row.MetadataJson
        };
    }

    private static NormalizedMovementsQuery NormalizeMovementsQuery(InventoryHistoryMovementsQuery? query)
    {
        query ??= new InventoryHistoryMovementsQuery();

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        if (pageSize > 200) pageSize = 200;

        var sortDir = string.IsNullOrWhiteSpace(query.SortDir)
            ? "desc"
            : query.SortDir.Trim().ToLowerInvariant();

        if (sortDir is not ("asc" or "desc"))
            throw new ArgumentException("sortDir must be asc or desc.");

        var itemType = NormalizeOptionalItemType(query.ItemType);
        if (query.ItemId.HasValue && itemType is null)
            throw new ArgumentException("itemType is required when itemId is provided.");

        if (query.ItemId.HasValue && query.ItemId.Value <= 0)
            throw new ArgumentException("itemId must be a positive integer.");

        if (query.BatchId.HasValue && query.BatchId.Value <= 0)
            throw new ArgumentException("batchId must be a positive integer.");

        if (query.DeliveryId.HasValue && query.DeliveryId.Value <= 0)
            throw new ArgumentException("deliveryId must be a positive integer.");

        var eventType = NormalizeOptionalEventType(query.EventType);
        var fromUtc = NormalizeOptionalUtc(query.FromUtc);
        var toUtc = NormalizeOptionalUtc(query.ToUtc);

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
            throw new ArgumentException("fromUtc must be less than or equal to toUtc.");

        return new NormalizedMovementsQuery(
            ItemType: itemType,
            ItemId: query.ItemId,
            BatchId: query.BatchId,
            EventType: eventType,
            DeliveryId: query.DeliveryId,
            FromUtc: fromUtc,
            ToUtc: toUtc,
            SortDir: sortDir,
            Page: page,
            PageSize: pageSize);
    }

    private static string? NormalizeOptionalItemType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim().ToUpperInvariant();
        if (!AllowedItemTypes.Contains(trimmed))
            throw new ArgumentException("itemType must be INGREDIENT or PRODUCT.");

        return trimmed;
    }

    private static string? NormalizeOptionalEventType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (!EventTypeMap.TryGetValue(trimmed, out var canonical))
            throw new ArgumentException("Unsupported eventType for inventory history filter.");

        return canonical;
    }

    private static DateTime? NormalizeOptionalUtc(DateTime? value)
        => value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : null;

    private static ItemEnrichment ResolveItemMeta(
        string itemType,
        int itemId,
        EnrichmentBundle enrichment)
    {
        if (itemType == InventoryHistoryItemTypes.Ingredient && enrichment.Ingredients.TryGetValue(itemId, out var ingredientMeta))
            return ingredientMeta;

        if (itemType == InventoryHistoryItemTypes.Product && enrichment.Products.TryGetValue(itemId, out var productMeta))
            return productMeta;

        return new ItemEnrichment(null, null);
    }

    private static string? ResolveScopeName(string scopeType, int scopeId, EnrichmentBundle enrichment)
    {
        if (scopeType == InventoryLedgerScopeTypes.Franchise)
            return enrichment.Franchises.TryGetValue(scopeId, out var franchiseName) ? franchiseName : null;

        if (scopeType == InventoryLedgerScopeTypes.CentralKitchen)
            return enrichment.CentralKitchens.TryGetValue(scopeId, out var centralKitchenName) ? centralKitchenName : null;

        return null;
    }

    private static string BuildDeliveryCode(int deliveryId)
        => $"DLV-{deliveryId:D6}";

    private static string BuildOrderCode(int storeOrderId)
        => $"SO-{storeOrderId:D6}";

    private sealed class BatchLifecycleLedgerComparer : IComparer<InventoryLedgerEntry>
    {
        public static BatchLifecycleLedgerComparer Instance { get; } = new();

        public int Compare(InventoryLedgerEntry? x, InventoryLedgerEntry? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            var occurredAtComparison = x.OccurredAtUtc.CompareTo(y.OccurredAtUtc);
            if (occurredAtComparison != 0)
                return occurredAtComparison;

            if (x.CorrelationId == y.CorrelationId)
            {
                var sequenceComparison = x.SequenceNo.CompareTo(y.SequenceNo);
                if (sequenceComparison != 0)
                    return sequenceComparison;
            }

            return x.InventoryLedgerEntryId.CompareTo(y.InventoryLedgerEntryId);
        }
    }

    private sealed record NormalizedMovementsQuery(
        string? ItemType,
        int? ItemId,
        int? BatchId,
        string? EventType,
        int? DeliveryId,
        DateTime? FromUtc,
        DateTime? ToUtc,
        string SortDir,
        int Page,
        int PageSize);

    private sealed record BatchIdentity(string ItemType, int ItemId);

    private sealed record ItemEnrichment(string? Name, string? Unit);

    private sealed class DeliveryEnrichment
    {
        public int DeliveryId { get; set; }
        public string Status { get; set; } = default!;
    }

    private sealed class StoreOrderEnrichment
    {
        public int StoreOrderId { get; set; }
        public string Status { get; set; } = default!;
    }

    private sealed class CurrentBatchState
    {
        public int BatchId { get; set; }
        public string BatchCode { get; set; } = default!;
        public decimal Quantity { get; set; }
        public bool IsInTransit { get; set; }
        public int? DeliveryId { get; set; }
    }

    private sealed record EnrichmentBundle(
        IReadOnlyDictionary<int, string> Users,
        IReadOnlyDictionary<int, DeliveryEnrichment> Deliveries,
        IReadOnlyDictionary<int, StoreOrderEnrichment> StoreOrders,
        IReadOnlyDictionary<int, ItemEnrichment> Ingredients,
        IReadOnlyDictionary<int, ItemEnrichment> Products,
        IReadOnlyDictionary<int, string> Franchises,
        IReadOnlyDictionary<int, string> CentralKitchens);
}

