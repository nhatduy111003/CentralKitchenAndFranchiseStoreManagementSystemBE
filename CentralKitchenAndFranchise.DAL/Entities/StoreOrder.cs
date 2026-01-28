namespace CentralKitchenAndFranchise.DAL.Entities;

public class StoreOrder
{
    public int Id { get; set; }
    public int StoreFranchiseId { get; set; }
    public string Status { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }

    public Franchise StoreFranchise { get; set; } = default!;
    public ICollection<StoreOrderItem> Items { get; set; } = new List<StoreOrderItem>();
}
