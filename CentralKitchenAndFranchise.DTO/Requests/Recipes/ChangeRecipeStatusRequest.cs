namespace CentralKitchenAndFranchise.DTO.Requests.Recipes;

public class ChangeRecipeStatusRequest
{
    // DRAFT | ACTIVE | INACTIVE
    public string Status { get; set; } = default!;
    public string? Reason { get; set; }
}