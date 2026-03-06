namespace CentralKitchenAndFranchise.DTO.Requests.Rbac;

public class RoleListQuery
{
    public string? Status { get; set; } // ACTIVE / INACTIVE / ALL
    public string? Q { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public string? SortBy { get; set; } // name, createdAt, updatedAt, id
    public string? SortDir { get; set; } // asc / desc
}