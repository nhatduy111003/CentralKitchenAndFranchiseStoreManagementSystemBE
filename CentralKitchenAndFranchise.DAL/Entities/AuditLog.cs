namespace CentralKitchenAndFranchise.DAL.Entities;

public class AuditLog
{
    public int AuditLogId { get; set; }

    public int? UserId { get; set; }
    public int? FranchiseId { get; set; }
    public int? CentralKitchenId { get; set; }
    public string Action { get; set; } = null!;

    public string? EntityName { get; set; }
    public int? EntityId { get; set; }

    public string? OldDataJson { get; set; }
    public string? NewDataJson { get; set; }

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }
    public CentralKitchen? CentralKitchen { get; set; }
    public User? User { get; set; }
    public Franchise? Franchise { get; set; }
}
