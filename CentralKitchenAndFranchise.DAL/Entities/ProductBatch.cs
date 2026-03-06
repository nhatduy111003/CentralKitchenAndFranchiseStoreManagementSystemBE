namespace CentralKitchenAndFranchise.DAL.Entities;

public class ProductBatch
{
	public int BatchId { get; set; }
	public int ProductId { get; set; }
	public int FranchiseId { get; set; }

	public string BatchCode { get; set; } = default!;
	public decimal Quantity { get; set; }
	public DateOnly? ExpiredAt { get; set; }

	public DateTime CreatedAt { get; set; }

	public Product Product { get; set; } = default!;
	public Franchise Franchise { get; set; } = default!;
	public ICollection<ProductMovement> ProductMovements { get; set; } = new List<ProductMovement>();
}
