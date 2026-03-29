namespace CentralKitchenAndFranchise.DAL.Entities;

public class ProductBatch
{
    public int BatchId { get; set; }
    public int ProductId { get; set; }
    public int? ProductionRunId { get; set; }

    // destination inventory can exist at franchise or central kitchen
    public int? FranchiseId { get; set; }
    public Franchise? Franchise { get; set; } = default!;

    public int? CentralKitchenId { get; set; }
    public CentralKitchen? CentralKitchen { get; set; }

    public string BatchCode { get; set; } = default!;
    public decimal Quantity { get; set; }

    public bool IsInTransit { get; set; }
    public int? DeliveryId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Product Product { get; set; } = default!;
    public ProductionRun? ProductionRun { get; set; }
    public ICollection<ProductMovement> ProductMovements { get; set; } = new List<ProductMovement>();
}