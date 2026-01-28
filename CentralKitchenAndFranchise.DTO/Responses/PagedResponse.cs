namespace CentralKitchenAndFranchise.DTO.Responses;

public class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long Total { get; set; }
}
