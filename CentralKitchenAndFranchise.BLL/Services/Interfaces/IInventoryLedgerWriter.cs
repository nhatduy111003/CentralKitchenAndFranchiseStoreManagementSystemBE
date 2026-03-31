using CentralKitchenAndFranchise.BLL.Services.Models.InventoryHistory;
using CentralKitchenAndFranchise.DAL.Entities;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces;

public interface IInventoryLedgerWriter
{
    Task<IReadOnlyList<InventoryLedgerEntry>> AppendAsync(
        InventoryLedgerWriteRequest request,
        CancellationToken ct = default);
}