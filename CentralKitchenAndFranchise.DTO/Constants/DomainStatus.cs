namespace CentralKitchenAndFranchise.DTO.Constants;

public static class IngredientStatus
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
}

public static class DeliveryStatus
{
    public const string Created = "CREATED";
    public const string Shipped = "SHIPPING";
    public const string Delivered = "DELIVERED";
    public const string Confirmed = "CONFIRMED";
    public const string Cancelled = "CANCELLED";
}

public static class MovementType
{
    public const string In = "IN";
    public const string Out = "OUT";
    public const string Waste = "WASTE";
    public const string Adjust = "ADJUST";
}

public static class ProductStatus
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
}

public static class StoreCatalogStatus
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
}

public static class ProductTypes
{
    public const string Finished = "FINISHED";
    public const string SemiFinished = "SEMI_FINISHED";
}
public static class InventoryOwnerType
{
    public const string Franchise = "FRANCHISE";
    public const string CentralKitchen = "CENTRAL_KITCHEN";
}

public static class InventoryMovementType
{
    public const string In = "IN";
    public const string Out = "OUT";
    public const string Adjust = "ADJUST";
    public const string Waste = "WASTE";
}

public static class BomStatus
{
    public const string Active = "ACTIVE";
}

public static class OrganizationStatus
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
}

public static class AuditAction
{
    // Ingredient inventory
    public const string IngredientInboundCreate = "INGREDIENT_INBOUND_CREATE";
    public const string IngredientIssueByProductionPlan = "INGREDIENT_ISSUE_BY_PRODUCTION_PLAN";
    public const string IngredientAdjust = "INGREDIENT_ADJUST";
    public const string IngredientWaste = "INGREDIENT_WASTE";

    // Product inventory
    public const string ProductInboundCreate = "PRODUCT_INBOUND_CREATE";

    // Production plan
    public const string ProductionPlanCreate = "PRODUCTION_PLAN_CREATE";
    public const string ProductionPlanStatusUpdate = "PRODUCTION_PLAN_STATUS_UPDATE";
}