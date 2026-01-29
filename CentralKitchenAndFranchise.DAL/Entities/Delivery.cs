namespace CentralKitchenAndFranchise.DAL.Entities;

public class Delivery
{
    public int DeliveryId { get; set; }
    public int DeliveryPlanId { get; set; }

    // migration full dùng DateTime (timestamptz)
    public DateTime DeliveredAt { get; set; }

    public DeliveryPlan DeliveryPlan { get; set; } = default!;
    public ICollection<ReceivingReport> ReceivingReports { get; set; } = new List<ReceivingReport>();
}
