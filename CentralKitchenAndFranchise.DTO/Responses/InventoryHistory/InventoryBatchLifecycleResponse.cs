namespace CentralKitchenAndFranchise.DTO.Responses.InventoryHistory;

public class InventoryBatchLifecycleResponse
{
    public int BatchId { get; set; }

    public string ItemType { get; set; } = default!;
    public int ItemId { get; set; }
    public string? ItemName { get; set; }
    public string? ItemUnit { get; set; }

    public string ScopeType { get; set; } = default!;
    public int ScopeId { get; set; }
    public string? ScopeName { get; set; }

    // Historical identity from ledger snapshots.
    public string? BatchCode { get; set; }
    public DateTime? BatchCreatedAtUtc { get; set; }
    public DateOnly? ExpiredAt { get; set; }

    // Current state is enrich-only and must not be used to interpret historical rows.
    public bool CurrentBatchExists { get; set; }
    public string? CurrentBatchCode { get; set; }
    public decimal? CurrentQuantity { get; set; }
    public bool? CurrentIsInTransit { get; set; }
    public string? CurrentBucket { get; set; }
    public int? CurrentDeliveryId { get; set; }
    public string? CurrentDeliveryCode { get; set; }

    public List<InventoryHistoryMovementResponse> Timeline { get; set; } = new();
}
