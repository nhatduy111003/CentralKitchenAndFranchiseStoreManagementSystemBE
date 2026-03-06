namespace CentralKitchenAndFranchise.DTO.Responses.Deliveries;

public class DeliveryProductItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public decimal Quantity { get; set; }
}

public class DeliveryIngredientItemDto
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = default!;
    public decimal Quantity { get; set; }
}
