namespace CentralKitchenAndFranchise.DTO.Responses.Receivings;

public class ReceivingListItemResponse
{
    public int ReceivingId { get; set; }
    public string DeliveryCode { get; set; } = default!;

    public int FranchiseId { get; set; }
    public int CentralKitchenId { get; set; }
    public string CentralKitchenName { get; set; } = default!;

    public DateOnly PlanDate { get; set; }
    public DateTime DeliveryDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = default!;
    public bool CanConfirm { get; set; }

    public int TotalItems { get; set; }
    public decimal TotalQuantity { get; set; }

    public int? StoreOrderId { get; set; }
    public string? OrderCode { get; set; }
}
