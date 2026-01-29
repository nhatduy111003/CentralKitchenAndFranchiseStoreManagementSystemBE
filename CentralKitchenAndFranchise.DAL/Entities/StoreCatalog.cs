namespace CentralKitchenAndFranchise.DAL.Entities;

public class StoreCatalog
{
    public int FranchiseId { get; set; }
    public int ProductId { get; set; }
    public decimal Price { get; set; }

    public Franchise Franchise { get; set; } = default!;
    public Product Product { get; set; } = default!;
}
