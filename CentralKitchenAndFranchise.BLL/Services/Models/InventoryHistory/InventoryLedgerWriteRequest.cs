namespace CentralKitchenAndFranchise.BLL.Services.Models.InventoryHistory;

public sealed class InventoryLedgerWriteRequest
{
    public Guid? CorrelationId { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public bool SaveChanges { get; set; }

    public IList<InventoryLedgerWriteItem> Items { get; set; } = new List<InventoryLedgerWriteItem>();
}