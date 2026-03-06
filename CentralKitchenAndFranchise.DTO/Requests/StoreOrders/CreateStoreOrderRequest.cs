namespace CentralKitchenAndFranchise.DTO.Requests.StoreOrders;

public class CreateStoreOrderRequest
{
    public DateOnly OrderDate { get; set; }
    public List<CreateStoreOrderItemRequest> Items { get; set; } = new();
}

public class CreateStoreOrderItemRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
}