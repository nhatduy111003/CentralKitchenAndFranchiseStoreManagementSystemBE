namespace CentralKitchenAndFranchise.DTO.Requests.StoreOrders;

public class UpdateStoreOrderRequest
{
    public DateOnly OrderDate { get; set; }

    // product / semi-product lines
    public List<UpdateStoreOrderItemRequest> Items { get; set; } = new();

    // ingredient lines
    public List<UpdateStoreOrderIngredientItemRequest> IngredientItems { get; set; } = new();
}

public class UpdateStoreOrderItemRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
}

public class UpdateStoreOrderIngredientItemRequest
{
    public int IngredientId { get; set; }
    public decimal Quantity { get; set; }
}