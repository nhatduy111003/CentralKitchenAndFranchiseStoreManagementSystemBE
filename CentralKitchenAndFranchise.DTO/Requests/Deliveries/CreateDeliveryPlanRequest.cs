namespace CentralKitchenAndFranchise.DTO.Requests.Deliveries;

public class CreateDeliveryPlanRequest
{
    public int ToFranchiseId { get; set; }
    public DateOnly PlannedDate { get; set; }
}
