namespace CentralKitchenAndFranchise.DAL.Entities;

public class UserFranchise
{
    public int UserId { get; set; }
    public int FranchiseId { get; set; }

    // migration full dùng DateTime (timestamptz)
    public DateTime AssignedAt { get; set; }

    public User User { get; set; } = default!;
    public Franchise Franchise { get; set; } = default!;
}
