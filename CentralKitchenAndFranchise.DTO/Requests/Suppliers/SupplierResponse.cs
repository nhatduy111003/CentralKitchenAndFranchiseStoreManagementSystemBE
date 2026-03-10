namespace CentralKitchenAndFranchise.DTO.Responses.Suppliers;

public class SupplierResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? ContactInfo { get; set; }
    public string Status { get; set; } = default!;
}
