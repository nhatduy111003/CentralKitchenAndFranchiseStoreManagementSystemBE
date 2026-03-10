namespace CentralKitchenAndFranchise.DTO.Requests.AuditLogs;

public class AuditLogListQuery
{
    public string? Q { get; set; }              // search action/entity/reason
    public int? UserId { get; set; }
    public int? FranchiseId { get; set; }
    public string? EntityName { get; set; }
    public string? Action { get; set; }

    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }

    public string? SortBy { get; set; }         // createdAt | action | entityName
    public string? SortDir { get; set; }        // asc | desc

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}