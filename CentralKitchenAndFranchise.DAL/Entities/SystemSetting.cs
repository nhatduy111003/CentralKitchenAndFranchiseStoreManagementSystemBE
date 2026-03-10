namespace CentralKitchenAndFranchise.DAL.Entities;

public class SystemSetting
{
    public int SystemSettingId { get; set; }   // int identity
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
