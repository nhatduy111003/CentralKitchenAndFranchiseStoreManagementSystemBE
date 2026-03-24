namespace CentralKitchenAndFranchise.DTO.Responses.Dashboard;

public class ManagerDashboardOverviewResponse
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int TimezoneOffsetMinutes { get; set; }

    // Admin/Manager are global roles, so this is the number of active franchises in scope.
    public int FranchiseCount { get; set; }

    // Order summary uses StoreOrder.OrderDate as business date.
    public OrderStatusSummary OrderStatusSummary { get; set; } = new();

    // Delivery summary uses DeliveryPlan.PlannedDate as business date.
    public DeliveryStatusSummary DeliveryStatusSummary { get; set; } = new();

    public ServiceLevelSummary ServiceLevelSummary { get; set; } = new();

    public List<LowStockAlertItem> LowStockAlerts { get; set; } = new();
    public List<NearExpiryAlertItem> NearExpiryAlerts { get; set; } = new();
    public List<WasteAlertItem> WasteAlerts { get; set; } = new();

    public List<string> Notes { get; set; } = new();
}

public class OrderStatusSummary
{
    public int Total { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class DeliveryStatusSummary
{
    public int Total { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int PendingCount { get; set; }
    public int DeliveredCount { get; set; }
    public int DeliveredPendingReceivingCount { get; set; }
    public int ConfirmedReceivingCount { get; set; }
}

public class ServiceLevelSummary
{
    public int? TotalDeliveriesPlannedInRange { get; set; }
    public int? TotalDeliveriesDeliveredInRange { get; set; }

    public int? OnTimeDeliveredCount { get; set; }
    public decimal? OnTimeRate { get; set; }
}

public class LowStockAlertItem
{
    public int FranchiseId { get; set; }
    public string FranchiseName { get; set; } = default!;

    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = default!;
    public string Unit { get; set; } = default!;

    public decimal OnHandQuantity { get; set; }
    public decimal SafetyStock { get; set; }
}

public class NearExpiryAlertItem
{
    public int FranchiseId { get; set; }
    public string FranchiseName { get; set; } = default!;

    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = default!;
    public string Unit { get; set; } = default!;

    public int BatchId { get; set; }
    public string BatchCode { get; set; } = default!;
    public decimal Quantity { get; set; }
    public DateOnly? ExpiredAt { get; set; }
    public int? DaysToExpire { get; set; }
}

public class WasteAlertItem
{
    public int FranchiseId { get; set; }
    public string FranchiseName { get; set; } = default!;

    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = default!;
    public string Unit { get; set; } = default!;

    public decimal WasteQuantity { get; set; }
    public decimal IssuedQuantity { get; set; }
    public decimal? WasteRate { get; set; }
    public decimal WasteThreshold { get; set; }
    public bool IsExceedThreshold { get; set; }
}   