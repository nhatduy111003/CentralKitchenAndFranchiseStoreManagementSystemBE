namespace CentralKitchenAndFranchise.DTO.Constants;

public static class StandardizationStatuses
{
    public const string Draft = "DRAFT";
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";

    public static bool IsValid(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var v = s.Trim().ToUpperInvariant();
        return v is Draft or Active or Inactive;
    }
}