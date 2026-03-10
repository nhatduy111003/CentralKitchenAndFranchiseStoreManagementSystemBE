namespace CentralKitchenAndFranchise.DAL.Entities;

public class Franchise
{
    public int FranchiseId { get; set; }
    public string Name { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? Address { get; set; }
    public string? Location { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public int CentralKitchenId { get; set; }
    public CentralKitchen CentralKitchen { get; set; } = default!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<UserWorkAssignment> WorkAssignments { get; set; } = new List<UserWorkAssignment>();
    public ICollection<StoreCatalog> StoreCatalogs { get; set; } = new List<StoreCatalog>();
    public ICollection<StoreOrder> StoreOrders { get; set; } = new List<StoreOrder>();
    public ICollection<IngredientBatch> IngredientBatches { get; set; } = new List<IngredientBatch>();

    public ICollection<SalesRecord> SalesRecords { get; set; } = new List<SalesRecord>();
}