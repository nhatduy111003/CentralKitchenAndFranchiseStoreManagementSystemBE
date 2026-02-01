using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.Ingredients;

public class CreateIngredientRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = default!;

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Unit { get; set; } = default!;

    [Range(typeof(decimal), "0", "1000")]
    public decimal SafetyStock { get; set; } = 0;

    // thường là % hoặc ngưỡng; tạm cho phép 0..100
    [Range(typeof(decimal), "0", "100")]
    public decimal WasteThreshold { get; set; } = 0;
}
