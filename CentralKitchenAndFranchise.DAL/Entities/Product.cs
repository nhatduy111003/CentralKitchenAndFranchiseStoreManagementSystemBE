namespace CentralKitchenAndFranchise.DAL.Entities;

public class Product
{
    public int ProductId { get; set; }
    public string Name { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public string Status { get; set; } = "ACTIVE";

    public ICollection<StoreCatalog> StoreCatalogs { get; set; } = new List<StoreCatalog>();
    public ICollection<StoreOrderItem> StoreOrderItems { get; set; } = new List<StoreOrderItem>();
    public ICollection<Bom> Boms { get; set; } = new List<Bom>();
    public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
    public ICollection<ProductionPlanItem> ProductionPlanItems { get; set; } = new List<ProductionPlanItem>();
    public ICollection<SalesRecord> SalesRecords { get; set; } = new List<SalesRecord>();
    public ICollection<DemandItem> DemandItems { get; set; } = new List<DemandItem>();
    public ICollection<AllocationItem> AllocationItems { get; set; } = new List<AllocationItem>();
}
