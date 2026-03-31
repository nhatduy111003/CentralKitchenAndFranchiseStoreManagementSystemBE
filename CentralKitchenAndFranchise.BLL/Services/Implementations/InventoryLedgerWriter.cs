using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.BLL.Services.Models.InventoryHistory;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;

namespace CentralKitchenAndFranchise.BLL.Services.Implementations;

public sealed class InventoryLedgerWriter : IInventoryLedgerWriter
{
    private static readonly HashSet<string> AllowedItemTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        InventoryHistoryItemTypes.Ingredient,
        InventoryHistoryItemTypes.Product
    };

    private static readonly HashSet<string> AllowedScopeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        InventoryLedgerScopeTypes.Franchise,
        InventoryLedgerScopeTypes.CentralKitchen
    };

    private static readonly HashSet<string> AllowedBuckets = new(StringComparer.OrdinalIgnoreCase)
    {
        InventoryLedgerStockBuckets.OnHand,
        InventoryLedgerStockBuckets.Transit
    };

    private static readonly HashSet<string> AllowedEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        InventoryLedgerEventTypes.Inbound,
        InventoryLedgerEventTypes.Adjust,
        InventoryLedgerEventTypes.Waste,
        InventoryLedgerEventTypes.IssueProd,
        InventoryLedgerEventTypes.PrepareOut,
        InventoryLedgerEventTypes.TransitIn,
        InventoryLedgerEventTypes.TransitOut,
        InventoryLedgerEventTypes.ReceiveIn,
        InventoryLedgerEventTypes.Rename,
        InventoryLedgerEventTypes.Archive,
        InventoryLedgerEventTypes.Reverse
    };

    private static readonly HashSet<string> AllowedReferenceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        InventoryLedgerReferenceTypes.Manual,
        InventoryLedgerReferenceTypes.Delivery,
        InventoryLedgerReferenceTypes.Receiving,
        InventoryLedgerReferenceTypes.ProductionPlan,
        InventoryLedgerReferenceTypes.ProductionRun,
        InventoryLedgerReferenceTypes.Batch,
        InventoryLedgerReferenceTypes.System
    };

    private readonly AppDbContext _db;

    public InventoryLedgerWriter(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<InventoryLedgerEntry>> AppendAsync(
        InventoryLedgerWriteRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("Ledger write request must contain at least one item.", nameof(request));

        var occurredAtUtc = NormalizeOccurredAtUtc(request.OccurredAtUtc);
        var correlationId = request.CorrelationId ?? Guid.NewGuid();
        var sequenceNumbers = ResolveSequenceNumbers(request.Items);

        var entries = new List<InventoryLedgerEntry>(request.Items.Count);

        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            ValidateItem(item, i);

            entries.Add(new InventoryLedgerEntry
            {
                CorrelationId = correlationId,
                SequenceNo = sequenceNumbers[i],
                OccurredAtUtc = occurredAtUtc,
                ItemType = NormalizeRequired(item.ItemType, nameof(item.ItemType)),
                ItemId = item.ItemId,
                BatchId = item.BatchId,
                BatchCodeSnapshot = NormalizeOptional(item.BatchCodeSnapshot),
                BatchCreatedAtUtc = NormalizeOptionalUtc(item.BatchCreatedAtUtc),
                ExpiredAtSnapshot = item.ExpiredAtSnapshot,
                ScopeType = NormalizeRequired(item.ScopeType, nameof(item.ScopeType)),
                ScopeId = item.ScopeId,
                StockBucket = NormalizeRequired(item.StockBucket, nameof(item.StockBucket)),
                DeltaQuantity = item.DeltaQuantity,
                EventType = NormalizeRequired(item.EventType, nameof(item.EventType)),
                Reason = NormalizeOptional(item.Reason),
                ActorUserId = item.ActorUserId,
                ReferenceType = NormalizeOptional(item.ReferenceType),
                ReferenceId = item.ReferenceId,
                DeliveryId = item.DeliveryId,
                DeliveryPlanId = item.DeliveryPlanId,
                StoreOrderId = item.StoreOrderId,
                RequestedQuantitySnapshot = item.RequestedQuantitySnapshot,
                ActualQuantitySnapshot = item.ActualQuantitySnapshot,
                DroppedQuantitySnapshot = item.DroppedQuantitySnapshot,
                DropReasonSnapshot = NormalizeOptional(item.DropReasonSnapshot),
                CounterpartyScopeType = NormalizeOptional(item.CounterpartyScopeType),
                CounterpartyScopeId = item.CounterpartyScopeId,
                CounterpartyBatchId = item.CounterpartyBatchId,
                IsNonStockEvent = item.IsNonStockEvent,
                MetadataJson = NormalizeOptional(item.MetadataJson)
            });
        }

        _db.InventoryLedgerEntries.AddRange(entries);

        if (request.SaveChanges)
            await _db.SaveChangesAsync(ct);

        return entries;
    }

    private static void ValidateItem(InventoryLedgerWriteItem item, int index)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.ItemId <= 0)
            throw new ArgumentException($"Ledger item at index {index} must have ItemId > 0.");

        if (item.ScopeId <= 0)
            throw new ArgumentException($"Ledger item at index {index} must have ScopeId > 0.");

        if (!AllowedItemTypes.Contains(item.ItemType))
            throw new ArgumentException($"Ledger item at index {index} has unsupported ItemType '{item.ItemType}'.");

        if (!AllowedScopeTypes.Contains(item.ScopeType))
            throw new ArgumentException($"Ledger item at index {index} has unsupported ScopeType '{item.ScopeType}'.");

        if (!AllowedBuckets.Contains(item.StockBucket))
            throw new ArgumentException($"Ledger item at index {index} has unsupported StockBucket '{item.StockBucket}'.");

        if (!AllowedEventTypes.Contains(item.EventType))
            throw new ArgumentException($"Ledger item at index {index} has unsupported EventType '{item.EventType}'.");

        if (!string.IsNullOrWhiteSpace(item.ReferenceType) && !AllowedReferenceTypes.Contains(item.ReferenceType))
            throw new ArgumentException($"Ledger item at index {index} has unsupported ReferenceType '{item.ReferenceType}'.");

        if (!string.IsNullOrWhiteSpace(item.CounterpartyScopeType) && !AllowedScopeTypes.Contains(item.CounterpartyScopeType))
            throw new ArgumentException($"Ledger item at index {index} has unsupported CounterpartyScopeType '{item.CounterpartyScopeType}'.");

        if (item.BatchId.HasValue && item.BatchId.Value <= 0)
            throw new ArgumentException($"Ledger item at index {index} has invalid BatchId.");

        if (item.ActorUserId.HasValue && item.ActorUserId.Value <= 0)
            throw new ArgumentException($"Ledger item at index {index} has invalid ActorUserId.");

        if (item.ReferenceId.HasValue && item.ReferenceId.Value <= 0)
            throw new ArgumentException($"Ledger item at index {index} has invalid ReferenceId.");

        if (item.DeliveryId.HasValue && item.DeliveryId.Value <= 0)
            throw new ArgumentException($"Ledger item at index {index} has invalid DeliveryId.");

        if (item.DeliveryPlanId.HasValue && item.DeliveryPlanId.Value <= 0)
            throw new ArgumentException($"Ledger item at index {index} has invalid DeliveryPlanId.");

        if (item.StoreOrderId.HasValue && item.StoreOrderId.Value <= 0)
            throw new ArgumentException($"Ledger item at index {index} has invalid StoreOrderId.");

        if (item.CounterpartyScopeId.HasValue && item.CounterpartyScopeId.Value <= 0)
            throw new ArgumentException($"Ledger item at index {index} has invalid CounterpartyScopeId.");

        if (item.CounterpartyBatchId.HasValue && item.CounterpartyBatchId.Value <= 0)
            throw new ArgumentException($"Ledger item at index {index} has invalid CounterpartyBatchId.");

        if (item.BatchCreatedAtUtc.HasValue && item.BatchCreatedAtUtc.Value.Kind != DateTimeKind.Utc)
            throw new ArgumentException($"Ledger item at index {index} must provide BatchCreatedAtUtc in UTC.");

        if (item.IsNonStockEvent && item.DeltaQuantity != 0m)
            throw new ArgumentException($"Ledger item at index {index} is marked as non-stock but DeltaQuantity is not 0.");
    }

    private static int[] ResolveSequenceNumbers(IList<InventoryLedgerWriteItem> items)
    {
        var hasExplicit = items.Any(x => x.SequenceNo.HasValue);

        if (!hasExplicit)
            return Enumerable.Range(1, items.Count).ToArray();

        if (items.Any(x => !x.SequenceNo.HasValue || x.SequenceNo.Value <= 0))
            throw new ArgumentException("When any ledger item specifies SequenceNo, all items must specify a positive SequenceNo.");

        var duplicates = items
            .GroupBy(x => x.SequenceNo!.Value)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
            throw new ArgumentException($"Duplicate SequenceNo values are not allowed: {string.Join(", ", duplicates)}.");

        return items.Select(x => x.SequenceNo!.Value).ToArray();
    }

    private static DateTime NormalizeOccurredAtUtc(DateTime occurredAtUtc)
    {
        if (occurredAtUtc == default)
            throw new ArgumentException("OccurredAtUtc is required and must be UTC.");

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("OccurredAtUtc must be provided in UTC.");

        return occurredAtUtc;
    }

    private static DateTime? NormalizeOptionalUtc(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        if (value.Value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Optional UTC timestamps on ledger rows must be provided in UTC.");

        return value.Value;
    }

    private static string NormalizeRequired(string value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{propertyName} is required.");

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

