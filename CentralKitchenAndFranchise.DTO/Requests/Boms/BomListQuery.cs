namespace CentralKitchenAndFranchise.DTO.Requests.Boms;

public class BomListQuery
{
    public int? ProductId { get; set; }
    public string? Status { get; set; } // DRAFT|ACTIVE|INACTIVE|ALL

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public string? SortBy { get; set; } = "id";    // id|productId|version|status|createdAt|updatedAt
    public string? SortDir { get; set; } = "desc"; // asc|desc
}