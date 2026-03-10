namespace CentralKitchenAndFranchise.DAL.Entities;

public class Permission
{
    public int PermissionId { get; set; }

    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string GroupName { get; set; } = null!;
    public string Description { get; set; } = null!;

    // Soft delete
    public string Status { get; set; } = "ACTIVE"; // ACTIVE / INACTIVE

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}