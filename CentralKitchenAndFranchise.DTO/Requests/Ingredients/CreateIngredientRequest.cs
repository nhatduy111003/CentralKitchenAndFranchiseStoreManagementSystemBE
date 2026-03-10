using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.Ingredients;

public class CreateIngredientRequest
{
    [Required(ErrorMessage = "Tên nguyên liệu không được để trống")]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = default!;

    [Required(ErrorMessage = "Đơn vị tính không được để trống")]
    [StringLength(50, MinimumLength = 1)]
    public string Unit { get; set; } = default!;

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal Price { get; set; } = 0;

    [Range(typeof(decimal), "0", "1000")]
    public decimal SafetyStock { get; set; } = 0;

    [Range(typeof(decimal), "0", "100")]
    public decimal WasteThreshold { get; set; } = 0;
}
