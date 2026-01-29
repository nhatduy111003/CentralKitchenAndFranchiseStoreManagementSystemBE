namespace CentralKitchenAndFranchise.DAL.Entities;

public class ProductionPlanItem
{
    public int ProductionPlanItemId { get; set; }
    public int ProductionPlanId { get; set; }
    public int ProductId { get; set; }

    public decimal Quantity { get; set; }

    public ProductionPlan ProductionPlan { get; set; } = default!;
    public Product Product { get; set; } = default!;
}
