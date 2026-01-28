namespace CentralKitchenAndFranchise.DAL.Entities;

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? ContactInfo { get; set; }
    public string Status { get; set; } = "ACTIVE";
}
