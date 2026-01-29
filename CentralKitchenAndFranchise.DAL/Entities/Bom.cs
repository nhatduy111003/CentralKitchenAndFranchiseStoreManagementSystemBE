namespace CentralKitchenAndFranchise.DAL.Entities;

public class Bom
{
    public int BomId { get; set; }
    public int ProductId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = default!;

    // migration full dùng DateTime (timestamptz)
    public DateTime CreatedAt { get; set; }

    public Product Product { get; set; } = default!;
    public ICollection<BomItem> Items { get; set; } = new List<BomItem>();
}
