namespace CentralKitchenAndFranchise.DTO.Requests.InventoryHistory;

public class InventoryBatchLifecycleQuery
{
    // Optional disambiguator because IngredientBatch and ProductBatch use separate identity spaces.
    public string? ItemType { get; set; }
}
