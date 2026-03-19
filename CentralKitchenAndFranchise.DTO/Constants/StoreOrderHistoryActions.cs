namespace CentralKitchenAndFranchise.DTO.Constants;

public static class StoreOrderHistoryActions
{
    public const string OrderCreated = "ORDER_CREATED";
    public const string OrderLocked = "ORDER_LOCKED";
    public const string OrderReceivedByKitchen = "ORDER_RECEIVED_BY_KITCHEN";
    public const string ProcessingNoteUpdated = "PROCESSING_NOTE_UPDATED";
    public const string OrderForwardedToSupply = "ORDER_FORWARDED_TO_SUPPLY";
    public const string OrderPreparing = "ORDER_PREPARING";
    public const string DeliveryStatusChanged = "DELIVERY_STATUS_CHANGED";
    public const string OrderReceivedByStore = "ORDER_RECEIVED_BY_STORE";
    public const string OrderCancelled = "ORDER_CANCELLED";
}