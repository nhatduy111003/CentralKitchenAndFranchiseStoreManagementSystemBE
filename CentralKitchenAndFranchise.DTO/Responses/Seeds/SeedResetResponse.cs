namespace CentralKitchenAndFranchise.DTO.Responses.Seed;

public class SeedResetResponse
{
    public bool ResetDone { get; set; }
    public bool ReseededBaseData { get; set; }
    public int TablesTruncated { get; set; }
    public List<string> TruncatedTables { get; set; } = new();

    public List<SeedAccountInfo> DefaultAccounts { get; set; } = new();
}

public class SeedAccountInfo
{
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Role { get; set; } = default!;
}