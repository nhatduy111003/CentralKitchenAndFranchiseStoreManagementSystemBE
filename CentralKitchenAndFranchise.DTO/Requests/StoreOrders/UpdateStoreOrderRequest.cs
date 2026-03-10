namespace CentralKitchenAndFranchise.DTO.Requests.StoreOrders;

public class UpdateStoreOrderRequest
{
    public DateOnly OrderDate { get; set; }
    public List<UpdateStoreOrderItemRequest> Items { get; set; } = new();
}

public class UpdateStoreOrderItemRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
}