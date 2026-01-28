namespace CentralKitchenAndFranchise.DAL.Entities;

public class ProductionPlan
{
    public int Id { get; set; }
    public DateOnly PlanDate { get; set; }
    public int FranchiseId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Franchise Franchise { get; set; } = default!;
    public ICollection<ProductionPlanItem> Items { get; set; } = new List<ProductionPlanItem>();
}
