namespace CentralKitchenAndFranchise.DTO.Requests.Suppliers;

public class UpdateSupplierRequest
{
    public string Name { get; set; } = default!;
    public string? ContactInfo { get; set; }
}
