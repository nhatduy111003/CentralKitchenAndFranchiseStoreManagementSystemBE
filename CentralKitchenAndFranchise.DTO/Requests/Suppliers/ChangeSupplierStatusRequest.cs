namespace CentralKitchenAndFranchise.DTO.Requests.Suppliers;

public class ChangeSupplierStatusRequest
{
    // ACTIVE / INACTIVE
    public string Status { get; set; } = default!;
    public string? Reason { get; set; }
}
