using CentralKitchenAndFranchise.DTO.Responses.Inventory;

namespace CentralKitchenAndFranchise.DTO.Responses.StoreOrders;

public class IncomingOrderDetailResponse
{
    public int StoreOrderId { get; set; }
    public string OrderCode { get; set; } = default!;
    public string Status { get; set; } = default!;

    public DateOnly RequestedDeliveryDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public string? StoreNote { get; set; }

    public int StoreId { get; set; }
    public string StoreName { get; set; } = default!;
    public string? StoreAddress { get; set; }

    public int TotalItems { get; set; }
    public decimal TotalQuantity { get; set; }

    public int ForwardedTotalItems { get; set; }
    public decimal ForwardedTotalQuantity { get; set; }
    public int DroppedTotalItems { get; set; }
    public decimal DroppedTotalQuantity { get; set; }

    public DateTime? ReceivedAt { get; set; }
    public string? ReceivedBy { get; set; }

    public DateTime? ForwardedAt { get; set; }
    public string? ForwardedBy { get; set; }

    public string? ProcessingNote { get; set; }
    public string? ForwardNote { get; set; }

    public List<IncomingOrderDetailItemResponse> Items { get; set; } = new();
}

public class IncomingOrderDetailItemResponse
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public string? Sku { get; set; }
    public string Unit { get; set; } = default!;

    // original locked order quantity
    public decimal Quantity { get; set; }
    public string? ProductStatus { get; set; }

    // effective sanitized values for FE main rendering
    public decimal ForwardedQuantity { get; set; }
    public decimal DroppedQuantity { get; set; }
    public bool IsDroppedFromForward { get; set; }
    public string? DropReason { get; set; }

    // raw delivery snapshot data before BE consistency sanitization
    public bool HasForwardSnapshot { get; set; }
    public bool IsForwardSnapshotConsistent { get; set; }
    public string? ForwardSnapshotWarning { get; set; }
    public decimal RawForwardSnapshotRequestedQuantity { get; set; }
    public decimal RawForwardSnapshotForwardedQuantity { get; set; }
    public decimal RawForwardSnapshotDroppedQuantity { get; set; }

    public decimal AvailableInCentralKitchenQuantity { get; set; }
    public bool IsSufficientInCentralKitchen { get; set; }

    public List<InventoryBatchQuantityResponse> AvailableCentralKitchenBatches { get; set; } = new();
}