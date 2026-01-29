namespace CentralKitchenAndFranchise.DAL.Entities;

public class StoreOrder
{
    public int StoreOrderId { get; set; }
    public int FranchiseId { get; set; }

    public string Status { get; set; } = default!;

    // migration full dùng DateTime (timestamptz)
    public DateTime CreatedAt { get; set; }

    public Franchise Franchise { get; set; } = default!;
    public ICollection<StoreOrderItem> Items { get; set; } = new List<StoreOrderItem>();
}
