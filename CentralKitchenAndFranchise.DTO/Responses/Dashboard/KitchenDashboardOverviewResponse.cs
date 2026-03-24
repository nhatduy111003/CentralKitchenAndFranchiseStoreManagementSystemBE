namespace CentralKitchenAndFranchise.DTO.Responses.Dashboard;

public class KitchenDashboardOverviewResponse
{
    public int CentralKitchenId { get; set; }
    public string CentralKitchenName { get; set; } = default!;

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int TimezoneOffsetMinutes { get; set; }

    public int ManagedFranchiseCount { get; set; }

    public KitchenOrderQueueSummary OrderQueueSummary { get; set; } = new();
    public KitchenProductionPlanSummary ProductionPlanSummary { get; set; } = new();
    public KitchenProductionRunSummary ProductionRunSummary { get; set; } = new();

    public List<KitchenLowStockAlertItem> LowStockAlerts { get; set; } = new();
    public List<KitchenNearExpiryAlertItem> NearExpiryAlerts { get; set; } = new();
    public List<KitchenActionItem> PriorityActions { get; set; } = new();

    public List<string> Notes { get; set; } = new();
}

public class KitchenOrderQueueSummary
{
    public int Total { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int LockedCount { get; set; }
    public int ReceivedByKitchenCount { get; set; }
    public int ForwardedToSupplyCount { get; set; }
    public int OverdueActionCount { get; set; }
}

public class KitchenProductionPlanSummary
{
    public int Total { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int DueTodayOpenCount { get; set; }
    public int OverdueOpenCount { get; set; }
    public decimal TotalPlannedQuantity { get; set; }
}

public class KitchenProductionRunSummary
{
    public int Total { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public decimal TotalRunQuantity { get; set; }
    public decimal CompletedQuantity { get; set; }
}

public class KitchenLowStockAlertItem
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public decimal OnHandQuantity { get; set; }
    public decimal SafetyStock { get; set; }
}

public class KitchenNearExpiryAlertItem
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public int BatchId { get; set; }
    public string BatchCode { get; set; } = default!;
    public decimal Quantity { get; set; }
    public DateOnly? ExpiredAt { get; set; }
    public int? DaysToExpire { get; set; }
}

public class KitchenActionItem
{
    public string ActionType { get; set; } = default!;
    public string Message { get; set; } = default!;

    public int RelatedId { get; set; }
    public string RelatedCode { get; set; } = default!;

    public DateOnly? BusinessDate { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
}