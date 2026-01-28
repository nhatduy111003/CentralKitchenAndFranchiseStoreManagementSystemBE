namespace CentralKitchenAndFranchise.DAL.Entities;

public class StoreOrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }

    public StoreOrder Order { get; set; } = default!;
    public Product Product { get; set; } = default!;
}
