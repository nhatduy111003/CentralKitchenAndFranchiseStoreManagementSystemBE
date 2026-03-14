namespace CentralKitchenAndFranchise.DAL.Entities;

public class CentralKitchen
{
    public int CentralKitchenId { get; set; }

    public string Name { get; set; } = default!;
    public string Status { get; set; } = default!;

    public string? Address { get; set; }
    public string? Location { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Franchise> Franchises { get; set; }
        = new List<Franchise>();

    public ICollection<IngredientBatch> IngredientBatches { get; set; }
        = new List<IngredientBatch>();

    public ICollection<ProductBatch> ProductBatches { get; set; }
        = new List<ProductBatch>();

    public ICollection<ProductionPlan> ProductionPlans { get; set; }
        = new List<ProductionPlan>();

    public ICollection<DeliveryPlan> DeliveryPlans { get; set; }
        = new List<DeliveryPlan>();

    public ICollection<AllocationItem> AllocationItems { get; set; }
        = new List<AllocationItem>();

    public ICollection<UserWorkAssignment> WorkAssignments { get; set; }
    = new List<UserWorkAssignment>();

    public ICollection<ProductionRun> ProductionRuns { get; set; } = new List<ProductionRun>();

}