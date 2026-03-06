namespace CentralKitchenAndFranchise.DAL.Entities;

public class Role
{
    public int RoleId { get; set; }
    public string Name { get; set; } = default!;

    // Soft delete
    public string Status { get; set; } = "ACTIVE"; // ACTIVE / INACTIVE

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}