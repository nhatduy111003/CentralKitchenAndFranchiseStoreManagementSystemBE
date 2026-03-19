using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.StoreOrders;

public class UpdateProcessingNoteRequest
{
    [Required]
    [MaxLength(1000)]
    public string ProcessingNote { get; set; } = default!;
}
