using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.Inventory;

public class CreateIngredientBatchDto
{
    [Range(1, int.MaxValue)]
    public int IngredientId { get; set; }

    [Required]
    public string BatchCode { get; set; } = default!;

    [Range(typeof(decimal), "0.000001", "79228162514264337593543950335")]
    public decimal Quantity { get; set; }

    // Optional để backfill mẻ đã sản xuất/nhập trước đó.
    public DateTime? CreatedAtUtc { get; set; }

    public string? Reason { get; set; }
}