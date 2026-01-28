namespace CentralKitchenAndFranchise.DAL.Entities;

public class UserFranchise
{
    public int UserId { get; set; }
    public int FranchiseId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }

    public User User { get; set; } = default!;
    public Franchise Franchise { get; set; } = default!;
}
