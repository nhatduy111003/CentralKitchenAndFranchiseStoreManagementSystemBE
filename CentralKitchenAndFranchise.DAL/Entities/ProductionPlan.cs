using CentralKitchenAndFranchise.DAL.Enums;

namespace CentralKitchenAndFranchise.DAL.Entities;

public class ProductionPlan
{
    public int ProductionPlanId { get; set; }
    public DateOnly PlanDate { get; set; }
    public int FranchiseId { get; set; }

    public ProductionPlanStatus? Status { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdateAt { get; set; }

    public Franchise Franchise { get; set; } = default!;
    public ICollection<ProductionPlanItem> Items { get; set; } = new List<ProductionPlanItem>();
    public ICollection<ProductionBatch> ProductionBatches { get; set; } = new List<ProductionBatch>();
}