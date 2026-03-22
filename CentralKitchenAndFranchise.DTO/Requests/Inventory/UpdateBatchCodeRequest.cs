using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.Inventory;

public class UpdateBatchCodeRequest
{
    [Required]
    public string BatchCode { get; set; } = default!;

    public string? Reason { get; set; }
}