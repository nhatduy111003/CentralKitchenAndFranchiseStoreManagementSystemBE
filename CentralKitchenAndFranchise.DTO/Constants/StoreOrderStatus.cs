namespace CentralKitchenAndFranchise.DTO.Constants;

public static class StoreOrderStatus
{
    // Existing flow
    public const string Draft = "DRAFT";
    public const string Submitted = "SUBMITTED";
    public const string Locked = "LOCKED";
    public const string Cancelled = "CANCELLED";

    // New phase 1 workflow
    public const string ReceivedByKitchen = "RECEIVED_BY_KITCHEN";
    public const string ForwardedToSupply = "FORWARDED_TO_SUPPLY";

    public const string Preparing = "PREPARING";
    public const string ReadyToDeliver = "READY_TO_DELIVER";
    public const string InTransit = "IN_TRANSIT";
    public const string Delivered = "DELIVERED";

    public const string ReceivedByStore = "RECEIVED_BY_STORE";

    public static readonly string[] All =
    [
        Draft,
        Submitted,
        Locked,
        Cancelled,
        ReceivedByKitchen,
        ForwardedToSupply,
        Preparing,
        ReadyToDeliver,
        InTransit,
        Delivered,
        ReceivedByStore
    ];

    public static bool IsValid(string status)
        => All.Contains(status?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
}