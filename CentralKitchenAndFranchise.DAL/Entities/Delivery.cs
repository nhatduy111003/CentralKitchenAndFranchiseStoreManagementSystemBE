namespace CentralKitchenAndFranchise.DAL.Entities;

public class Delivery
{
    public int DeliveryId { get; set; }

    public int DeliveryPlanId { get; set; }

    // source central kitchen
    public int FromCentralKitchenId { get; set; }

    public string Status { get; set; } = "CREATED";

    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    // sửa từ non-null -> nullable
    public DateTime? DeliveredAt { get; set; }

    public DeliveryPlan DeliveryPlan { get; set; } = default!;
    public CentralKitchen FromCentralKitchen { get; set; } = default!;

    public ICollection<DeliveryProductItem> ProductItems { get; set; } = new List<DeliveryProductItem>();
    public ICollection<DeliveryIngredientItem> IngredientItems { get; set; } = new List<DeliveryIngredientItem>();
    public ICollection<ReceivingReport> ReceivingReports { get; set; } = new List<ReceivingReport>();
}