namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class Bom
    {
        public int BomId { get; set; }
        public int ProductId { get; set; }
        public int Version { get; set; }
        public string Status { get; set; } = "DRAFT";

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<BomItem> Items { get; set; } = new List<BomItem>();
    }
}