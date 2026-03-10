namespace CentralKitchenAndFranchise.DAL.Entities;

public class UserWorkAssignment
{
    public int UserWorkAssignmentId { get; set; }

    public int UserId { get; set; }

    public string AssignmentType { get; set; } = default!;

    public int? FranchiseId { get; set; }
    public int? CentralKitchenId { get; set; }

    public DateTime AssignedAt { get; set; }

    public User User { get; set; } = default!;
    public Franchise? Franchise { get; set; }
    public CentralKitchen? CentralKitchen { get; set; }
}