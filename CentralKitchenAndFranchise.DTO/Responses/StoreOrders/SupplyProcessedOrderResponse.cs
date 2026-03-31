namespace CentralKitchenAndFranchise.DTO.Responses.StoreOrders;

public class SupplyProcessedOrderResponse
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

    public DateTime? ForwardedAt { get; set; }
    public string? ForwardedBy { get; set; }

    public DateTime? PreparedAt { get; set; }
    public string? PreparedBy { get; set; }

    public DateTime EndedAt { get; set; }
    public string? EndedBy { get; set; }
    public string? EndedNote { get; set; }

    public string? ForwardNote { get; set; }
    public string? ProcessingNote { get; set; }
    public string? PreparingNote { get; set; }
}