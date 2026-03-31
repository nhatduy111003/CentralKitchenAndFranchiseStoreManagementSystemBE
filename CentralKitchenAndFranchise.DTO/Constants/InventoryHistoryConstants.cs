namespace CentralKitchenAndFranchise.DTO.Constants;

public static class InventoryHistoryItemTypes
{
    public const string Ingredient = "INGREDIENT";
    public const string Product = "PRODUCT";
}

public static class InventoryLedgerScopeTypes
{
    public const string Franchise = "FRANCHISE";
    public const string CentralKitchen = "CENTRAL_KITCHEN";
}

public static class InventoryLedgerStockBuckets
{
    public const string OnHand = "ON_HAND";
    public const string Transit = "TRANSIT";
}

public static class InventoryLedgerEventTypes
{
    public const string Inbound = "Inbound";
    public const string Adjust = "Adjust";
    public const string Waste = "Waste";
    public const string IssueProd = "IssueProd";
    public const string PrepareOut = "PrepareOut";
    public const string TransitIn = "TransitIn";
    public const string TransitOut = "TransitOut";
    public const string ReceiveIn = "ReceiveIn";
    public const string Rename = "Rename";
    public const string Archive = "Archive";
    public const string Reverse = "Reverse";
}

public static class InventoryLedgerReferenceTypes
{
    public const string Manual = "MANUAL";
    public const string Delivery = "DELIVERY";
    public const string Receiving = "RECEIVING";
    public const string ProductionPlan = "PRODUCTION_PLAN";
    public const string ProductionRun = "PRODUCTION_RUN";
    public const string Batch = "BATCH";
    public const string System = "SYSTEM";
}