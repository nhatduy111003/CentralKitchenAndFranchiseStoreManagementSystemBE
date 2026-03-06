namespace CentralKitchenAndFranchise.DTO.Responses.Seed;

public class SeedSampleDataResponse
{
    public bool AlreadySeeded { get; set; }

    public int FranchisesCreated { get; set; }
    public int UsersCreated { get; set; }
    public int SuppliersCreated { get; set; }
    public int IngredientsCreated { get; set; }
    public int ProductsCreated { get; set; }
    public int StoreCatalogItemsCreated { get; set; }

    public List<int> FranchiseIds { get; set; } = new();
    public List<int> UserIds { get; set; } = new();
    public List<int> ProductIds { get; set; } = new();
    public List<int> IngredientIds { get; set; } = new();
    public List<int> SupplierIds { get; set; } = new();
}