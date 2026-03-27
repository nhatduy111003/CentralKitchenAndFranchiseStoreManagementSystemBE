using CentralKitchenAndFranchise.DTO.Constants;

namespace CentralKitchenAndFranchise.BLL.Extensions;

public sealed class ResolvedForwardSnapshotLine
{
    public int ItemId { get; init; }
    public bool HasSnapshot { get; init; }
    public bool IsConsistent { get; init; }
    public string? Warning { get; init; }

    public decimal RawRequestedQuantity { get; init; }
    public decimal RawForwardedQuantity { get; init; }
    public decimal RawDroppedQuantity { get; init; }

    public decimal ForwardedQuantity { get; init; }
    public decimal DroppedQuantity { get; init; }
    public bool IsDropped { get; init; }
    public string? DropReason { get; init; }
}

public static class StoreOrderForwardSnapshotHelper
{
    public static bool ShouldExposeForwardSnapshot(string orderStatus)
        => orderStatus is StoreOrderStatus.ForwardedToSupply
            or StoreOrderStatus.Preparing
            or StoreOrderStatus.ReadyToDeliver
            or StoreOrderStatus.InTransit
            or StoreOrderStatus.Delivered
            or StoreOrderStatus.ReceivedByStore;

    public static ResolvedForwardSnapshotLine Resolve(
        string orderStatus,
        string itemKeyLabel,
        int itemId,
        decimal orderQuantity,
        bool hasSnapshot,
        decimal snapshotRequestedQuantity,
        decimal snapshotForwardedQuantity,
        bool snapshotIsDropped,
        string? snapshotDropReason)
    {
        var rawDropped = Math.Max(snapshotRequestedQuantity - snapshotForwardedQuantity, 0m);
        var shouldExpose = ShouldExposeForwardSnapshot(orderStatus);

        if (!hasSnapshot)
        {
            return new ResolvedForwardSnapshotLine
            {
                ItemId = itemId,
                HasSnapshot = false,
                IsConsistent = !shouldExpose,
                Warning = shouldExpose
                    ? $"Forward snapshot is missing for {itemKeyLabel}={itemId} while order status is {orderStatus}."
                    : null
            };
        }

        string? warning = null;

        if (!shouldExpose)
        {
            warning = $"Forward snapshot already exists for {itemKeyLabel}={itemId} while order status is {orderStatus}.";
        }
        else if (snapshotRequestedQuantity <= 0)
        {
            warning = $"Forward snapshot requested quantity is invalid for {itemKeyLabel}={itemId}.";
        }
        else if (snapshotRequestedQuantity != orderQuantity)
        {
            warning = $"Forward snapshot requested quantity ({snapshotRequestedQuantity}) does not match current order quantity ({orderQuantity}) for {itemKeyLabel}={itemId}.";
        }
        else if (snapshotForwardedQuantity > snapshotRequestedQuantity)
        {
            warning = $"Forward snapshot forwarded quantity ({snapshotForwardedQuantity}) exceeds requested quantity ({snapshotRequestedQuantity}) for {itemKeyLabel}={itemId}.";
        }

        var isConsistent = string.IsNullOrWhiteSpace(warning);

        return new ResolvedForwardSnapshotLine
        {
            ItemId = itemId,
            HasSnapshot = true,
            IsConsistent = isConsistent,
            Warning = warning,
            RawRequestedQuantity = snapshotRequestedQuantity,
            RawForwardedQuantity = snapshotForwardedQuantity,
            RawDroppedQuantity = rawDropped,
            ForwardedQuantity = isConsistent ? snapshotForwardedQuantity : 0m,
            DroppedQuantity = isConsistent ? rawDropped : 0m,
            IsDropped = isConsistent && snapshotIsDropped,
            DropReason = isConsistent ? snapshotDropReason : null
        };
    }
}