namespace CentralKitchenAndFranchise.DAL.Entities;

public class ProductionPlan
{
    public int ProductionPlanId { get; set; }
    public DateOnly PlanDate { get; set; }
    public int FranchiseId { get; set; }

    // migration full dùng DateTime (timestamptz)
    public DateTime CreatedAt { get; set; }

    public Franchise Franchise { get; set; } = default!;
    public ICollection<ProductionPlanItem> Items { get; set; } = new List<ProductionPlanItem>();
    public ICollection<ProductionBatch> ProductionBatches { get; set; } = new List<ProductionBatch>();
}
