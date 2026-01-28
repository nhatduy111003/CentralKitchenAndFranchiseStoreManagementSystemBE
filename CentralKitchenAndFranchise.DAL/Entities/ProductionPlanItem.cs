namespace CentralKitchenAndFranchise.DAL.Entities;

public class ProductionPlanItem
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }

    public ProductionPlan Plan { get; set; } = default!;
    public Product Product { get; set; } = default!;
}
