namespace CentralKitchenAndFranchise.DTO.Requests.Rbac;

public class PermissionListQuery
{
    public string? Status { get; set; } // ACTIVE / INACTIVE / ALL
    public string? Q { get; set; } // search by Code/Name/GroupName

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public string? SortBy { get; set; } // code, name, groupName, createdAt, updatedAt, id
    public string? SortDir { get; set; } // asc / desc
}