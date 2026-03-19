namespace CentralKitchenAndFranchise.DAL.Entities;

public class ReceivingReport
{
    public int ReceivingReportId { get; set; }
    public int DeliveryId { get; set; }

    public DateTime ReceivedAt { get; set; }

    public int? ReceivedByUserId { get; set; }
    public string? Note { get; set; }

    public Delivery Delivery { get; set; } = null!;
}