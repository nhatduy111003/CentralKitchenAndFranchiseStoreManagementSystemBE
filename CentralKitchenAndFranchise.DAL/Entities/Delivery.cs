namespace CentralKitchenAndFranchise.DAL.Entities;

public class Delivery
{
    public int Id { get; set; }
    public int StoreFranchiseId { get; set; }
    public DateTimeOffset DeliveredAt { get; set; }

    public Franchise StoreFranchise { get; set; } = default!;
}
