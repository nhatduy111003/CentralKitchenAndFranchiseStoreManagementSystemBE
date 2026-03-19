namespace CentralKitchenAndFranchise.DTO.Requests.StoreOrders;

public class SupplyOrderListQuery
{
    public string? Status { get; set; }
    public int? FranchiseId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? Search { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public string? SortBy { get; set; } = "forwardedAt"; 
    public string? SortDir { get; set; } = "desc";
}
