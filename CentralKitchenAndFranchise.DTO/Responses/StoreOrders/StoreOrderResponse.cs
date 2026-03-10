namespace CentralKitchenAndFranchise.DTO.Responses.StoreOrders;

public class StoreOrderResponse
{
    public int StoreOrderId { get; set; }
    public int FranchiseId { get; set; }

    public string Status { get; set; } = default!;
    public DateOnly OrderDate { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? LockedAt { get; set; }

    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public List<StoreOrderItemResponse> Items { get; set; } = new();
}

public class StoreOrderItemResponse
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public decimal Quantity { get; set; }
}