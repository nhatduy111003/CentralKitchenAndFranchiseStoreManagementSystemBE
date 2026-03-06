namespace CentralKitchenAndFranchise.DTO.Requests.Suppliers;

public class CreateSupplierRequest
{
    public string Name { get; set; } = default!;
    public string? ContactInfo { get; set; }
}
