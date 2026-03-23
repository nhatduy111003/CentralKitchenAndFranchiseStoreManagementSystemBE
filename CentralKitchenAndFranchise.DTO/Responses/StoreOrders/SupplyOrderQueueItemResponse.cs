namespace CentralKitchenAndFranchise.DTO.Responses.StoreOrders;

public class SupplyOrderQueueItemResponse
{
    public int StoreOrderId { get; set; }
    public string OrderCode { get; set; } = default!;
    public string Status { get; set; } = default!;

    public DateOnly RequestedDeliveryDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public int StoreId { get; set; }
    public string StoreName { get; set; } = default!;

    public int TotalItems { get; set; }
    public decimal TotalQuantity { get; set; }

    public int ForwardedTotalItems { get; set; }
    public decimal ForwardedTotalQuantity { get; set; }
    public int DroppedTotalItems { get; set; }
    public decimal DroppedTotalQuantity { get; set; }

    public DateTime? ForwardedAt { get; set; }
    public string? ForwardedBy { get; set; }

    public string? ForwardNote { get; set; }
    public string? ProcessingNote { get; set; }

    public List<SupplyOrderQueueItemLineResponse> Items { get; set; } = new();
}

public class SupplyOrderQueueItemLineResponse
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public string? Sku { get; set; }
    public string Unit { get; set; } = default!;

    // original locked order quantity
    public decimal Quantity { get; set; }

    public decimal ForwardedQuantity { get; set; }
    public decimal DroppedQuantity { get; set; }
    public bool IsDroppedFromForward { get; set; }
    public string? DropReason { get; set; }
}