namespace CentralKitchenAndFranchise.DAL.Entities;

public class Franchise
{
    public int FranchiseId { get; set; }
    public string Name { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? Address { get; set; }
    public string? Location { get; set; }

    public ICollection<UserFranchise> UserFranchises { get; set; } = new List<UserFranchise>();

    public ICollection<StoreCatalog> StoreCatalogs { get; set; } = new List<StoreCatalog>();
    public ICollection<StoreOrder> StoreOrders { get; set; } = new List<StoreOrder>();
    public ICollection<IngredientBatch> IngredientBatches { get; set; } = new List<IngredientBatch>();
    public ICollection<ProductionPlan> ProductionPlans { get; set; } = new List<ProductionPlan>();

    public ICollection<DeliveryPlan> DeliveryPlans { get; set; } = new List<DeliveryPlan>();
    public ICollection<SalesRecord> SalesRecords { get; set; } = new List<SalesRecord>();

    public ICollection<AllocationItem> AllocationItems { get; set; } = new List<AllocationItem>();

    // IMPORTANT: Không có Deliveries trực tiếp vì bảng deliveries full DB KHÔNG có FranchiseId.
}
