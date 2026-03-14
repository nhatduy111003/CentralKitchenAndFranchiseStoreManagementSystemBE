namespace CentralKitchenAndFranchise.DTO.Responses.CentralKitchens;

public class CentralKitchenResponseDto
{
    public int CentralKitchenId { get; set; }
    public string Name { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? Address { get; set; }
    public string? Location { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public int FranchiseCount { get; set; }
    public int ActiveFranchiseCount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}