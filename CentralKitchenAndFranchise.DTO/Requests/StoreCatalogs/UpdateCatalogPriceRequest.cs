using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.StoreCatalog;

/// Body for PUT /api/franchises/{franchiseId}/catalog/{productId}/price
public class UpdateCatalogPriceRequest
{
    [Range(typeof(decimal), "0", "100000000")]
    public decimal Price { get; set; }
}
