namespace CentralKitchenAndFranchise.DTO.Responses.StoreOrders;

public class StoreOrderHistoryResponse
{
    public int HistoryId { get; set; }
    public int StoreOrderId { get; set; }

    public string ActionType { get; set; } = default!;
    public string ActionLabel { get; set; } = default!;

    public DateTime PerformedAt { get; set; }
    public string? PerformedBy { get; set; }

    public string? Note { get; set; }

    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
}
