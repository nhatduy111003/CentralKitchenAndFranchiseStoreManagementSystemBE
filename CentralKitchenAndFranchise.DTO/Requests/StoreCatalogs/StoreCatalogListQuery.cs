namespace CentralKitchenAndFranchise.DTO.Requests.StoreCatalogs;

public class StoreCatalogListQuery
{
    public int FranchiseId { get; set; }

    public string? Status { get; set; } // ACTIVE | INACTIVE | ALL
    public string? ProductType { get; set; } // FINISHED | SEMI_FINISHED

    public string? Q { get; set; } // search by product name/sku

    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    // sortBy: productName | sku | price | status | createdAt | updatedAt | productId
    public string? SortBy { get; set; } = "productName";
    public string? SortDir { get; set; } = "asc"; // asc | desc
}
