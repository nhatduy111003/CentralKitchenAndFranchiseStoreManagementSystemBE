using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.StoreCatalog;

/// Body for POST /api/franchises/{franchiseId}/catalog
public class AssignCatalogProductRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(typeof(decimal), "0", "100000000")]
    public decimal Price { get; set; }
}
