namespace CentralKitchenAndFranchise.DTO.Responses.Boms;

public class BomResponse
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<BomItemResponse> Items { get; set; } = new();
}

public class BomItemResponse
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = default!;
    public string IngredientUnit { get; set; } = default!;
    public decimal Quantity { get; set; }
}