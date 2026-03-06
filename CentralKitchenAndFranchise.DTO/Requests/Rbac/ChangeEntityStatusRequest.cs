namespace CentralKitchenAndFranchise.DTO.Requests.Rbac;

public class ChangeEntityStatusRequest
{
    public string Status { get; set; } = null!; // ACTIVE / INACTIVE
    public string? Reason { get; set; }
}