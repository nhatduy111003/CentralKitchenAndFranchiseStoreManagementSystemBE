using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.Products;

public class ProductStatusUpdateRequest
{
    // ACTIVE | INACTIVE
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = default!;
}