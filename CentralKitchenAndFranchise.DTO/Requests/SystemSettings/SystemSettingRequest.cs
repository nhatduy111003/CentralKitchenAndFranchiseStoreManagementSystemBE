using System.ComponentModel.DataAnnotations;

namespace CentralKitchenAndFranchise.DTO.Requests.SystemSettings;

public class SystemSettingRequest
{
    [Required]
    [StringLength(100)]
    public string Key { get; set; } = default!;

    [Required]
    [StringLength(200)]
    public string Value { get; set; } = default!;

    [StringLength(500)]
    public string? Description { get; set; }
}