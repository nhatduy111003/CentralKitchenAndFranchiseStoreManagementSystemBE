namespace CentralKitchenAndFranchise.DTO.Requests.StoreOrders;

public class SupplyOrderListQuery
{
    // ALL / DELIVERED / RECEIVED_BY_STORE / CANCELLED
    public string? Status { get; set; }

    public int? FranchiseId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? Search { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    // endedAt / forwardedAt / requestedDeliveryDate / storeName / status / createdAt
    public string? SortBy { get; set; } = "endedAt";

    // asc / desc
    public string? SortDir { get; set; } = "desc";
}