namespace CentralKitchenAndFranchise.DTO.Responses.StoreCatalog;

public class StoreCatalogResponse
{
    public int FranchiseId { get; set; }
    public string FranchiseName { get; set; } = default!;

    public int ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public string ProductType { get; set; } = default!;

    public decimal Price { get; set; }
    public string Status { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
