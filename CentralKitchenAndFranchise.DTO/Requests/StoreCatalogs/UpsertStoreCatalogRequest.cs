namespace CentralKitchenAndFranchise.DTO.Requests.StoreCatalogs;

public class UpsertStoreCatalogRequest
{
    public int FranchiseId { get; set; }
    public int ProductId { get; set; }
    public decimal Price { get; set; }
}
