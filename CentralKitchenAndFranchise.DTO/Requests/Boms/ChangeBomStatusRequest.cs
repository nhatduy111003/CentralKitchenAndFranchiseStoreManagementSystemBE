namespace CentralKitchenAndFranchise.DTO.Requests.Boms;

public class ChangeBomStatusRequest
{
    // DRAFT | ACTIVE | INACTIVE
    public string Status { get; set; } = default!;
    public string? Reason { get; set; }
}