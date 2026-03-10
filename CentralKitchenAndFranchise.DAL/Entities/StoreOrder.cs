namespace CentralKitchenAndFranchise.DAL.Entities;

public class StoreOrder
{
    public int StoreOrderId { get; set; }
    public int FranchiseId { get; set; }

    // DRAFT / SUBMITTED / LOCKED / CANCELLED / PROCESSING / COMPLETED ...
    public string Status { get; set; } = "DRAFT";

    // Store chọn ngày giao/nhận (business date)
    public DateOnly OrderDate { get; set; }

    // timestamps for tracking
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? LockedAt { get; set; }

    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public Franchise Franchise { get; set; } = default!;
    public ICollection<StoreOrderItem> Items { get; set; } = new List<StoreOrderItem>();
}