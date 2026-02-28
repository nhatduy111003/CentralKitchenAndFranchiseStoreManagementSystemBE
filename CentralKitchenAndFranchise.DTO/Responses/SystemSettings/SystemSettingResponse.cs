namespace CentralKitchenAndFranchise.DTO.Responses.SystemSettings;

public class SystemSettingResponse
{
    public int Id { get; set; }
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}