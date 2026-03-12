namespace CentralKitchenAndFranchise.DTO.Responses.Products;

public class ProductResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string ProductType { get; set; } = default!;
    public int ShelfLifeDays { get; set; }
}
