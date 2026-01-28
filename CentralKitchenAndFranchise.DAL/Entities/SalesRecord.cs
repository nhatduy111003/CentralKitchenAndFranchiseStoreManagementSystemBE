namespace CentralKitchenAndFranchise.DAL.Entities;

public class SalesRecord
{
    public int Id { get; set; }
    public int StoreFranchiseId { get; set; }
    public int ProductId { get; set; }
    public DateOnly SoldAt { get; set; }
    public decimal Quantity { get; set; }

    public Franchise StoreFranchise { get; set; } = default!;
    public Product Product { get; set; } = default!;
}
