namespace CentralKitchenAndFranchise.DTO.Requests.InventoryHistory;

public class InventoryHistoryMovementsQuery
{
    public string? ItemType { get; set; }
    public int? ItemId { get; set; }
    public int? BatchId { get; set; }
    public string? EventType { get; set; }
    public int? DeliveryId { get; set; }

    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }

    // desc = newest first, asc = oldest first
    public string? SortDir { get; set; } = "desc";

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
