namespace CentralKitchenAndFranchise.DTO.Responses.Rbac;

public class RoleResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}