namespace CentralKitchenAndFranchise.DTO.Requests.StoreCatalog;

/// Query params for GET /api/franchises/{franchiseId}/catalog
public class FranchiseCatalogListQuery
{
    public string? Status { get; set; } // ACTIVE | INACTIVE | ALL
    public string? Q { get; set; } // search by product name/sku

    // optional - keep to reuse existing service capabilities
    public string? ProductType { get; set; } // FINISHED | SEMI_FINISHED
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    // sortBy: productName | sku | price | status | createdAt | updatedAt | productId
    public string? SortBy { get; set; } = "productName";
    public string? SortDir { get; set; } = "asc"; // asc | desc
}
