namespace CentralKitchenAndFranchise.DAL.Entities;

public class ProductMovement
{
    public int MovementId { get; set; }
    public int BatchId { get; set; }

    public string Type { get; set; } = default!;
    public decimal Quantity { get; set; }

    public int? CreatedByUserId { get; set; }
    public string? Reason { get; set; }
    public int? DeliveryId { get; set; }

    public DateTime CreatedAt { get; set; }

    public ProductBatch Batch { get; set; } = default!;
}
