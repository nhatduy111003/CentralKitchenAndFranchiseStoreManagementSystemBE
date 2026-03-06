namespace CentralKitchenAndFranchise.DTO.Responses.AuditLogs;

public class AuditLogResponse
{
    public int Id { get; set; }

    public int? UserId { get; set; }
    public string? UserName { get; set; }

    public int? FranchiseId { get; set; }
    public string? FranchiseName { get; set; }

    public string Action { get; set; } = default!;
    public string? EntityName { get; set; }
    public int? EntityId { get; set; }

    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}