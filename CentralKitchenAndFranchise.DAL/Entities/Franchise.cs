namespace CentralKitchenAndFranchise.DAL.Entities;

public class Franchise
{
    public int Id { get; set; }
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
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
    public ICollection<SalesRecord> SalesRecords { get; set; } = new List<SalesRecord>();
}
