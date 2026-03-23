using CentralKitchenAndFranchise.DTO.Responses.Inventory;

public class DeliveryProductItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = default!;

    // actual forwarded quantity
    public decimal Quantity { get; set; }

    // original locked order quantity
    public decimal RequestedQuantity { get; set; }
    public decimal DroppedQuantity { get; set; }
    public bool IsDropped { get; set; }
    public string? DropReason { get; set; }

    public decimal AvailableInCentralKitchenQuantity { get; set; }
    public List<InventoryBatchQuantityResponse> AvailableCentralKitchenBatches { get; set; } = new();

    public decimal ShippedToFranchiseQuantity { get; set; }
    public List<InventoryBatchQuantityResponse> ShippedToFranchiseBatches { get; set; } = new();
}

public class DeliveryIngredientItemDto
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = default!;
    public decimal Quantity { get; set; }

    public decimal AvailableInCentralKitchenQuantity { get; set; }
    public List<InventoryBatchQuantityResponse> AvailableCentralKitchenBatches { get; set; } = new();

    public decimal ShippedToFranchiseQuantity { get; set; }
    public List<InventoryBatchQuantityResponse> ShippedToFranchiseBatches { get; set; } = new();
}