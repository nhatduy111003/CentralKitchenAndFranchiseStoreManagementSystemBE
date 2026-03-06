namespace CentralKitchenAndFranchise.DTO.Constants;

public static class SettingKeys
{
    public const string NearExpiryDays = "NEAR_EXPIRY_DAYS";

    // Ordering policies
    public const string FutureOrderLimitDays = "FUTURE_ORDER_LIMIT_DAYS";
    public const string OrderEditWindowMinutes = "ORDER_EDIT_WINDOW_MINUTES";
    public const string CutoffTime = "ORDER_CUTOFF_TIME"; // optional format "HH:mm"
}