namespace CentralKitchenAndFranchise.DTO.Responses.InventoryHistory;

public class InventoryHistoryMovementResponse
{
    public long InventoryLedgerEntryId { get; set; }
    public Guid CorrelationId { get; set; }
    public int SequenceNo { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    public string ItemType { get; set; } = default!;
    public int ItemId { get; set; }
    public string? ItemName { get; set; }
    public string? ItemUnit { get; set; }

    public int? BatchId { get; set; }
    public string? BatchCode { get; set; }
    public DateTime? BatchCreatedAtUtc { get; set; }
    public DateOnly? ExpiredAt { get; set; }

    public string ScopeType { get; set; } = default!;
    public int ScopeId { get; set; }
    public string? ScopeName { get; set; }

    public string StockBucket { get; set; } = default!;
    public decimal DeltaQuantity { get; set; }
    public string EventType { get; set; } = default!;
    public bool IsNonStockEvent { get; set; }
    public string? Reason { get; set; }

    public int? ActorUserId { get; set; }
    public string? ActorDisplay { get; set; }

    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }

    public int? DeliveryId { get; set; }
    public string? DeliveryCode { get; set; }
    public string? DeliveryStatus { get; set; }

    public int? DeliveryPlanId { get; set; }

    public int? StoreOrderId { get; set; }
    public string? OrderCode { get; set; }
    public string? OrderStatus { get; set; }

    public decimal? RequestedQuantitySnapshot { get; set; }
    public decimal? ActualQuantitySnapshot { get; set; }
    public decimal? DroppedQuantitySnapshot { get; set; }
    public string? DropReasonSnapshot { get; set; }

    public string? CounterpartyScopeType { get; set; }
    public int? CounterpartyScopeId { get; set; }
    public string? CounterpartyScopeName { get; set; }
    public int? CounterpartyBatchId { get; set; }

    public string? MetadataJson { get; set; }
}
