namespace CentralKitchenAndFranchise.DAL.Entities;

public class DeliveryProductItem
{
    public int DeliveryProductItemId { get; set; }

    public int DeliveryId { get; set; }
    public int ProductId { get; set; }

    // actual forwarded quantity
    public decimal Quantity { get; set; }

    // snapshot of locked order request
    public decimal RequestedQuantity { get; set; }

    // whole-line drop marker for partial forward flow
    public bool IsDropped { get; set; }
    public string? DropReason { get; set; }

    public Delivery Delivery { get; set; } = default!;
    public Product Product { get; set; } = default!;
}