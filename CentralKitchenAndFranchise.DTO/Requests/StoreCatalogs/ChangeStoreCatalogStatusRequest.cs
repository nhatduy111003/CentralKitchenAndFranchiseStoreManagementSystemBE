namespace CentralKitchenAndFranchise.DTO.Requests.StoreCatalog;

public class ChangeStoreCatalogStatusRequest
{
    public string Status { get; set; } = default!; // ACTIVE | INACTIVE
    public string? Reason { get; set; }
}
