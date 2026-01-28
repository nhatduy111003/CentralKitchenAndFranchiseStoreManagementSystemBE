namespace CentralKitchenAndFranchise.DAL.Entities;

public class StoreCatalog
{
    public int StoreFranchiseId { get; set; }
    public int ProductId { get; set; }
    public decimal Price { get; set; }

    public Franchise StoreFranchise { get; set; } = default!;
    public Product Product { get; set; } = default!;
}
