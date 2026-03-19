using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.StoreOrders;

public class UpdateSupplyDeliveryStatusRequest
{
    [Required]
    public string Status { get; set; } = default!;

    public string? StatusNote { get; set; }
}
