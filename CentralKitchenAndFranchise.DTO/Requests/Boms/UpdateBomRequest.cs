namespace CentralKitchenAndFranchise.DTO.Requests.Boms;

public class UpdateBomRequest
{
    public List<UpdateBomItemRequest> Items { get; set; } = new();
}

public class UpdateBomItemRequest
{
    public int IngredientId { get; set; }
    public decimal Quantity { get; set; }
}