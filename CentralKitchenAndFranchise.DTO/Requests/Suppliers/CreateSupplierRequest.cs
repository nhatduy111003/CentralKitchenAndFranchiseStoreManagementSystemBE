using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.Suppliers;

public class CreateSupplierRequest
{
    [Required(ErrorMessage = "Tên nhà cung cấp không được để trống")]
    public string Name { get; set; } = default!;
    public string? ContactInfo { get; set; }
}
