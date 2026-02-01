namespace CentralKitchenAndFranchise.DTO.Requests.Ingredients;

public class ChangeIngredientStatusRequest
{
    // ACTIVE / INACTIVE
    public string Status { get; set; } = default!;
    public string? Reason { get; set; }
}
