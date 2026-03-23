namespace CentralKitchenAndFranchise.DTO.Responses.StoreOrders;

public class OrderWorkflowActionResponse
{
    public int StoreOrderId { get; set; }
    public string Status { get; set; } = default!;

    public DateTime? ReceivedAt { get; set; }
    public string? ReceivedBy { get; set; }
    public string? ReceiveNote { get; set; }

    public string? ProcessingNote { get; set; }
    public DateTime? ProcessingNoteUpdatedAt { get; set; }
    public string? ProcessingNoteUpdatedBy { get; set; }

    public DateTime? ForwardedAt { get; set; }
    public string? ForwardedBy { get; set; }
    public string? ForwardNote { get; set; }

    public int ForwardedTotalItems { get; set; }
    public decimal ForwardedTotalQuantity { get; set; }
    public int DroppedTotalItems { get; set; }
    public decimal DroppedTotalQuantity { get; set; }
    public List<OrderForwardResultItemResponse> ForwardResultItems { get; set; } = new();

    public DateTime? PreparedAt { get; set; }
    public string? PreparedBy { get; set; }
    public string? PreparingNote { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public string? StatusNote { get; set; }

    public string Message { get; set; } = default!;
}

public class OrderForwardResultItemResponse
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public string? Sku { get; set; }
    public string Unit { get; set; } = default!;

    public decimal RequestedQuantity { get; set; }
    public decimal ForwardedQuantity { get; set; }
    public decimal DroppedQuantity { get; set; }

    public bool IsDropped { get; set; }
    public string? DropReason { get; set; }
}