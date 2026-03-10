namespace CentralKitchenAndFranchise.DAL.Entities;

public class Delivery
{
    public int DeliveryId { get; set; }

    public int DeliveryPlanId { get; set; }

    // Central Kitchen (From)
    public int FromFranchiseId { get; set; }

    public string Status { get; set; } = "CREATED";

    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    // tạm set = ConfirmedAt khi confirm
    public DateTime DeliveredAt { get; set; }

    public DeliveryPlan DeliveryPlan { get; set; } = default!;
    public Franchise FromFranchise { get; set; } = default!;

    public ICollection<DeliveryProductItem> ProductItems { get; set; } = new List<DeliveryProductItem>();
    public ICollection<DeliveryIngredientItem> IngredientItems { get; set; } = new List<DeliveryIngredientItem>();

    public ICollection<ReceivingReport> ReceivingReports { get; set; } = new List<ReceivingReport>();
}
