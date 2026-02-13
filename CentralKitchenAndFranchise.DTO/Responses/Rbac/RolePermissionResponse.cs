namespace CentralKitchenAndFranchise.DTO.Responses.Rbac;

public class RolePermissionResponse
{
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
    public string PermissionCode { get; set; } = null!;
    public string PermissionName { get; set; } = null!;
    public string GroupName { get; set; } = null!;
}