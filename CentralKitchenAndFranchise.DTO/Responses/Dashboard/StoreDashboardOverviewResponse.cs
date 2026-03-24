namespace CentralKitchenAndFranchise.DTO.Responses.Dashboard;

public class StoreDashboardOverviewResponse
{
    public int FranchiseId { get; set; }
    public string FranchiseName { get; set; } = default!;

    public int CentralKitchenId { get; set; }
    public string CentralKitchenName { get; set; } = default!;

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int TimezoneOffsetMinutes { get; set; }

    public StoreOrderSummary OrderSummary { get; set; } = new();
    public StoreReceivingSummary ReceivingSummary { get; set; } = new();
    public StoreInventorySummary InventorySummary { get; set; } = new();

    public List<StoreLowStockAlertItem> LowStockAlerts { get; set; } = new();
    public List<StoreNearExpiryAlertItem> NearExpiryAlerts { get; set; } = new();
    public List<StoreRecentDeliveryItem> RecentDeliveries { get; set; } = new();

    public List<string> Notes { get; set; } = new();
}

public class StoreOrderSummary
{
    public int Total { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int ActiveOrderCount { get; set; }
    public int DeliveredPendingReceivingCount { get; set; }
    public int ReceivedCount { get; set; }
}

public class StoreReceivingSummary
{
    public int PendingConfirmationCount { get; set; }
    public int ConfirmedCount { get; set; }

    public DateTime? LatestDeliveredAtUtc { get; set; }
    public DateTime? LatestConfirmedAtUtc { get; set; }
}

public class StoreInventorySummary
{
    public int IngredientItemCount { get; set; }
    public int ProductItemCount { get; set; }

    public int LowStockIngredientCount { get; set; }
    public int NearExpiryIngredientBatchCount { get; set; }

    public decimal TotalIngredientOnHand { get; set; }
    public decimal TotalProductOnHand { get; set; }
}

public class StoreLowStockAlertItem
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public decimal OnHandQuantity { get; set; }
    public decimal SafetyStock { get; set; }
}

public class StoreNearExpiryAlertItem
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

public class StoreRecentDeliveryItem
{
    public int DeliveryId { get; set; }
    public string DeliveryCode { get; set; } = default!;

    public DateOnly PlannedDate { get; set; }
    public string Status { get; set; } = default!;

    public DateTime CreatedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    public int TotalItems { get; set; }
    public decimal TotalQuantity { get; set; }
}