namespace CentralKitchenAndFranchise.DTO.Requests.StoreOrders;

public class StoreOrderListQuery
{
    public string? Status { get; set; } // DRAFT/SUBMITTED/LOCKED/CANCELLED/ALL
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public string? SortBy { get; set; } = "id";     // id/date/createdAt
    public string? SortDir { get; set; } = "desc";  // asc/desc
}