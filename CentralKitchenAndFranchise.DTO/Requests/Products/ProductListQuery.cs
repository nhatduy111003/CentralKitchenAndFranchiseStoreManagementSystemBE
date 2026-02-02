namespace CentralKitchenAndFranchise.DTO.Requests.Products;

public class ProductListQuery
{
    public string? Status { get; set; } // ACTIVE | INACTIVE | ALL
    public string? ProductType { get; set; } // FINISHED | SEMI_FINISHED

    public string? Q { get; set; } // search by name/sku

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    // sortBy: name | sku | unit | status | productType | id
    public string? SortBy { get; set; } = "name";
    public string? SortDir { get; set; } = "asc"; // asc | desc
}
