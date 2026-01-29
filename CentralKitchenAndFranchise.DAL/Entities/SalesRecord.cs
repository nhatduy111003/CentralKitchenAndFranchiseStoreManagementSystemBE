namespace CentralKitchenAndFranchise.DAL.Entities;

public class SalesRecord
{
    public int SalesRecordId { get; set; }
    public int FranchiseId { get; set; }
    public int ProductId { get; set; }

    public DateOnly SoldAt { get; set; }
    public decimal Quantity { get; set; }

    public Franchise Franchise { get; set; } = default!;
    public Product Product { get; set; } = default!;
}
