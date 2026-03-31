namespace CentralKitchenAndFranchise.DTO.Requests.Deliveries;

public class UpdateDeliveryItemQuantityRequest
{
    // Target shipped quantity for one existing delivery line.
    public decimal Quantity { get; set; }
}