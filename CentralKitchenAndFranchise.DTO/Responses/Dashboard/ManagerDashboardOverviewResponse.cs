// CentralKitchenAndFranchise.DTO/Responses/Dashboard/ManagerDashboardOverviewResponse.cs
namespace CentralKitchenAndFranchise.DTO.Responses.Dashboard;

public class ManagerDashboardOverviewResponse
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int TimezoneOffsetMinutes { get; set; }

    // Scope for Manager = assigned franchises; Admin = all active franchises
    public int FranchiseCount { get; set; }

    public OrderStatusSummary OrderStatusSummary { get; set; } = new();
    public DeliveryStatusSummary DeliveryStatusSummary { get; set; } = new();

    public ServiceLevelSummary ServiceLevelSummary { get; set; } = new();

    public List<LowStockAlertItem> LowStockAlerts { get; set; } = new();
    public List<NearExpiryAlertItem> NearExpiryAlerts { get; set; } = new();
    public List<WasteAlertItem> WasteAlerts { get; set; } = new();

    // Optional hints when some metrics have no data due to missing module/records.
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

    public int PendingCount { get; set; } // CREATED / CONFIRMED / PLANNED...
    public int DeliveredCount { get; set; } // DELIVERED / COMPLETED...
}

public class ServiceLevelSummary
{
    // Currently supported based on DeliveryPlan.PlannedDate and Delivery.DeliveredAt.
    // If no delivery data in range => nulls.
    public int? TotalDeliveriesPlannedInRange { get; set; }
    public int? TotalDeliveriesDeliveredInRange { get; set; }

    // DeliveredAt local date <= PlannedDate => on-time
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
    public decimal IssuedQuantity { get; set; } // OUT
    public decimal? WasteRate { get; set; } // Waste / (Waste + OUT)
    public decimal WasteThreshold { get; set; }
    public bool IsExceedThreshold { get; set; }
}