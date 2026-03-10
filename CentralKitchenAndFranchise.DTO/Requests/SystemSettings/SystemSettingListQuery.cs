namespace CentralKitchenAndFranchise.DTO.Requests.SystemSettings;

public class SystemSettingListQuery
{
    public string? Q { get; set; }          // search by Key/Description
    public string? SortBy { get; set; }     // key | createdAt | updatedAt
    public string? SortDir { get; set; }    // asc | desc

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}