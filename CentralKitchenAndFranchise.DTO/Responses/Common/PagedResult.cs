namespace CentralKitchenAndFranchise.DTO.Responses.Common;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }

    public static PagedResult<T> Create(List<T> items, int page, int pageSize, int totalItems)
    {
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        return new PagedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }
}
