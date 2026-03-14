
namespace CentralKitchenAndFranchise.DTO.Requests
{
    public class AssignUserWorkAssignmentDto
    {
        public int UserId { get; set; }
        public string AssignmentType { get; set; } = default!;
        public int? FranchiseId { get; set; }
        public int? CentralKitchenId { get; set; }
    }

}
