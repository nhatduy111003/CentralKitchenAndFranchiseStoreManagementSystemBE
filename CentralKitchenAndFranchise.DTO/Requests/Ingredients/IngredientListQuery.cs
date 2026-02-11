namespace CentralKitchenAndFranchise.DTO.Requests.Ingredients;

public class IngredientListQuery
{
    public string? Q { get; set; }              // search by name
    public string? Status { get; set; }         // ACTIVE / INACTIVE / ALL (default ACTIVE)
    public string? Unit { get; set; }           // exact match

    public int Page { get; set; } = 1;          // 1-based
    public int PageSize { get; set; } = 20;     // max 200

    public string? SortBy { get; set; } = "name";   // name, unit, price, createdAt, updatedAt, safetyStock, wasteThreshold, id
    public string? SortDir { get; set; } = "asc";   // asc/desc
}
