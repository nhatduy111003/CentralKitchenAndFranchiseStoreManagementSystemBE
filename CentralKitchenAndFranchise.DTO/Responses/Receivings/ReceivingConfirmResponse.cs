namespace CentralKitchenAndFranchise.DTO.Responses.Receivings;

public class ReceivingConfirmResponse
{
    public int ReceivingId { get; set; }
    public string DeliveryCode { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime ConfirmedAt { get; set; }
    public bool InventoryUpdated { get; set; }
}