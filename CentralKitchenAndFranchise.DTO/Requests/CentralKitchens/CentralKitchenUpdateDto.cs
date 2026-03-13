namespace CentralKitchenAndFranchise.DTO.Requests.CentralKitchens;

public class CentralKitchenUpdateDto
{
    public string Name { get; set; } = null!;
    public string? Status { get; set; }
    public string? Address { get; set; }
    public string? Location { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}