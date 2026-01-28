namespace CentralKitchenAndFranchise.DAL.Entities;

public class Bom
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }

    public Product Product { get; set; } = default!;
    public ICollection<BomItem> Items { get; set; } = new List<BomItem>();
}
