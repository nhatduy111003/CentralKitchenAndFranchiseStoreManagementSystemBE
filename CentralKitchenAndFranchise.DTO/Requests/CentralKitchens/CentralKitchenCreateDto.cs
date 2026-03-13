using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests.CentralKitchens;

public class CentralKitchenCreateDto
{
    public string Name { get; set; } = null!;
    public string? Status { get; set; } = "ACTIVE";
    public string? Address { get; set; }
    public string? Location { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}