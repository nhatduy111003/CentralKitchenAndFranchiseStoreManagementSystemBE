namespace CentralKitchenAndFranchise.DTO.Responses.Deliveries;

public class DeliveryDetailsResponse
{
    public int DeliveryId { get; set; }
    public int DeliveryPlanId { get; set; }

    public int FromCentralKitchenId { get; set; }
    public string FromCentralKitchenName { get; set; } = default!;

    public int ToFranchiseId { get; set; }
    public string ToFranchiseName { get; set; } = default!;

    public string Status { get; set; } = default!;
    public DateOnly PlannedDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }

    public List<DeliveryProductItemDto> ProductItems { get; set; } = new();
    public List<DeliveryIngredientItemDto> IngredientItems { get; set; } = new();
}
