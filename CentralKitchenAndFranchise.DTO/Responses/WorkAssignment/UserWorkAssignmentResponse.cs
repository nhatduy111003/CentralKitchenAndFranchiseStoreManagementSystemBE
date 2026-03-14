
namespace CentralKitchenAndFranchise.DTO.Responses.WorkAssignment
{
    public class UserWorkAssignmentResponse
    {
        public int UserId { get; set; }
        public string AssignmentType { get; set; } = default!;
        public int? FranchiseId { get; set; }
        public int? CentralKitchenId { get; set; }
        public DateTime AssignedAt { get; set; }
    }
}
