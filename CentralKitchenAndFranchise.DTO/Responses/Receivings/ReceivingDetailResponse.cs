namespace CentralKitchenAndFranchise.DTO.Responses.Receivings;

public class ReceivingDetailResponse
{
    public int ReceivingId { get; set; }
    public string DeliveryCode { get; set; } = default!;

    public string Status { get; set; } = default!;
    public bool CanConfirm { get; set; }

    public int CentralKitchenId { get; set; }
    public string CentralKitchenName { get; set; } = default!;

    public int FranchiseId { get; set; }
    public string FranchiseName { get; set; } = default!;

    public DateOnly PlanDate { get; set; }
    public DateTime DeliveryDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public string? Note { get; set; }

    public int? StoreOrderId { get; set; }
    public string? OrderCode { get; set; }

    public List<ReceivingDetailLineResponse> Items { get; set; } = new();
}

public class ReceivingDetailLineResponse
{
    public string ItemType { get; set; } = default!; // PRODUCT / INGREDIENT
    public int ItemId { get; set; }
    public string ItemName { get; set; } = default!;
    public string Unit { get; set; } = default!;

    public decimal ExpectedQuantity { get; set; }
    public decimal DeliveredQuantity { get; set; }

    public decimal? ReceivedQuantity { get; set; }
}