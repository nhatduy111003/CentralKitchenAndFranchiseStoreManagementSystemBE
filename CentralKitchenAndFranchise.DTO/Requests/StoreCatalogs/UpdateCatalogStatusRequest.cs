using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.StoreCatalog;

/// Body for PATCH /api/franchises/{franchiseId}/catalog/{productId}/status
public class UpdateCatalogStatusRequest
{
    [Required]
    public string Status { get; set; } = default!; // ACTIVE | INACTIVE

    public string? Reason { get; set; }
}
