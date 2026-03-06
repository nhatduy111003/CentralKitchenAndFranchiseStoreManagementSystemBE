using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.Ingredients;

public class ChangeIngredientStatusRequest
{
    // ACTIVE / INACTIVE
    [Required]
    [RegularExpression("^(ACTIVE|INACTIVE)$", ErrorMessage = "Status must be ACTIVE or INACTIVE.")]
    public string Status { get; set; } = default!;

    [StringLength(500)]
    public string? Reason { get; set; }
}
