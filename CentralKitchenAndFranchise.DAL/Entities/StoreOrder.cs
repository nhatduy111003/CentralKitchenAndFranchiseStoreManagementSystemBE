namespace CentralKitchenAndFranchise.DAL.Entities;

public class StoreOrder
{
    public int StoreOrderId { get; set; }
    public int FranchiseId { get; set; }

    // DRAFT / SUBMITTED / LOCKED / CANCELLED / PROCESSING / COMPLETED ...
    public string Status { get; set; } = "DRAFT";

    public string? StoreNote { get; set; }

    // Store chọn ngày giao/nhận (business date)
    public DateOnly OrderDate { get; set; }

    // timestamps for tracking
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? LockedAt { get; set; }

    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public DateTime? ReceivedAt { get; set; }
    public int? ReceivedByUserId { get; set; }
    public string? ReceiveNote { get; set; }

    public string? ProcessingNote { get; set; }
    public DateTime? ProcessingNoteUpdatedAt { get; set; }
    public int? ProcessingNoteUpdatedByUserId { get; set; }

    public DateTime? ForwardedAt { get; set; }
    public int? ForwardedByUserId { get; set; }
    public string? ForwardNote { get; set; }

    public DateTime? PreparedAt { get; set; }
    public int? PreparedByUserId { get; set; }
    public string? PreparingNote { get; set; }

    public DateTime? DeliveryStatusUpdatedAt { get; set; }
    public int? DeliveryStatusUpdatedByUserId { get; set; }
    public string? DeliveryStatusNote { get; set; }

    public Franchise Franchise { get; set; } = default!;
    public ICollection<StoreOrderItem> Items { get; set; } = new List<StoreOrderItem>();
    public ICollection<StoreOrderIngredientItem> IngredientItems { get; set; } = new List<StoreOrderIngredientItem>();
    public ICollection<StoreOrderHistory> Histories { get; set; } = new List<StoreOrderHistory>();

}