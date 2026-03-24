namespace CentralKitchenAndFranchise.DTO.Responses.Dashboard;

public class SupplyDashboardOverviewResponse
{
    public int CentralKitchenId { get; set; }
    public string CentralKitchenName { get; set; } = default!;

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int TimezoneOffsetMinutes { get; set; }

    public int ManagedFranchiseCount { get; set; }

    public SupplyOrderStatusSummary OrderStatusSummary { get; set; } = new();
    public SupplyDeliveryStatusSummary DeliveryStatusSummary { get; set; } = new();
    public SupplyDroppedLineSummary DroppedLineSummary { get; set; } = new();
    public SupplyReceivingSummary ReceivingSummary { get; set; } = new();

    public List<SupplyActionItem> PriorityActions { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

public class SupplyOrderStatusSummary
{
    public int Total { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int ForwardedToSupplyCount { get; set; }
    public int PreparingCount { get; set; }
    public int ReadyToDeliverCount { get; set; }
    public int InTransitCount { get; set; }
    public int DeliveredCount { get; set; }
}

public class SupplyDeliveryStatusSummary
{
    public int Total { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int DeliveredPendingReceivingCount { get; set; }
    public int ConfirmedReceivingCount { get; set; }
}

public class SupplyDroppedLineSummary
{
    public int OrdersWithDroppedLinesCount { get; set; }
    public int DroppedLinesCount { get; set; }
    public decimal DroppedQuantity { get; set; }
}

public class SupplyReceivingSummary
{
    public int PendingConfirmationCount { get; set; }
    public DateTime? LatestDeliveredAtUtc { get; set; }
    public DateTime? LatestConfirmedAtUtc { get; set; }
}

public class SupplyActionItem
{
    public string ActionType { get; set; } = default!;
    public string Message { get; set; } = default!;

    public int OrderId { get; set; }
    public string OrderCode { get; set; } = default!;

    public int FranchiseId { get; set; }
    public string FranchiseName { get; set; } = default!;

    public DateOnly BusinessDate { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
}