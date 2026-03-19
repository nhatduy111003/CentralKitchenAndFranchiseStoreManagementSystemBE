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
    public decimal Quantity { get; set; }
    public string? ProductStatus { get; set; }
}
