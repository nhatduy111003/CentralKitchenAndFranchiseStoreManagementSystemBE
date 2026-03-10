namespace CentralKitchenAndFranchise.DTO.Requests.Suppliers;

public class SupplierListQuery
{
    public string? Q { get; set; }                  // search by name/contact
    public string? Status { get; set; }             // ACTIVE / INACTIVE / ALL (default ACTIVE)

    public int Page { get; set; } = 1;              // 1-based
    public int PageSize { get; set; } = 20;         // max 200

    public string? SortBy { get; set; } = "name";   // name, status, id
    public string? SortDir { get; set; } = "asc";   // asc/desc
}
