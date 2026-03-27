namespace CentralKitchenAndFranchise.DTO.Requests.StoreOrders;

public class CreateStoreOrderRequest
{
    public DateOnly OrderDate { get; set; }

    // product / semi-product lines
    public List<CreateStoreOrderItemRequest> Items { get; set; } = new();

    // ingredient lines
    public List<CreateStoreOrderIngredientItemRequest> IngredientItems { get; set; } = new();
}

public class CreateStoreOrderItemRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
}

public class CreateStoreOrderIngredientItemRequest
{
    public int IngredientId { get; set; }
    public decimal Quantity { get; set; }
}