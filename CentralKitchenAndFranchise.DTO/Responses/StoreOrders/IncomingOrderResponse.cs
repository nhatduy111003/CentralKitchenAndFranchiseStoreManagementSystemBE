namespace CentralKitchenAndFranchise.DTO.Responses.StoreOrders;

public class IncomingOrderResponse
{
    public int StoreOrderId { get; set; }
    public int FranchiseId { get; set; }
    public string FranchiseName { get; set; } = default!;

    public string Status { get; set; } = default!;
    public DateOnly OrderDate { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? LockedAt { get; set; }

    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public List<IncomingOrderItemResponse> Items { get; set; } = new();
    public List<IncomingOrderIngredientItemResponse> IngredientItems { get; set; } = new();
}

public class IncomingOrderItemResponse
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public decimal Quantity { get; set; }
}

public class IncomingOrderIngredientItemResponse
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public decimal Quantity { get; set; }
}