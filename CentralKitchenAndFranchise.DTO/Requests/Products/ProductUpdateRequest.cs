using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.Products;

public class ProductUpdateRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = default!;

    [Required]
    [StringLength(100)]
    public string Sku { get; set; } = default!;

    [Required]
    [StringLength(50)]
    public string Unit { get; set; } = default!;

    // FINISHED | SEMI_FINISHED
    [Required]
    [StringLength(30)]
    public string ProductType { get; set; } = default!;

    [Range(1, int.MaxValue)]
    public int ShelfLifeDays { get; set; }
}