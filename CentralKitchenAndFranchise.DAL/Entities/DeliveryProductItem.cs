namespace CentralKitchenAndFranchise.DAL.Entities;

public class DeliveryProductItem
{
    public int DeliveryProductItemId { get; set; }

    public int DeliveryId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }

    public Delivery Delivery { get; set; } = default!;
    public Product Product { get; set; } = default!;
}
