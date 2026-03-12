using CentralKitchenAndFranchise.DTO.Requests;
using CentralKitchenAndFranchise.DTO.Responses.WorkAssignment;

namespace CentralKitchenAndFranchise.BLL.Services.Interfaces
{
    public interface IUserWorkAssignmentService
    {
        Task AssignAsync(AssignUserWorkAssignmentDto dto);

        Task<List<UserInWorkAssignmentDto>> GetUsersByAssignmentAsync(
            string assignmentType,
            int? franchiseId,
            int? centralKitchenId);

        Task RemoveAsync(int userId);

        Task<UserWorkAssignmentResponse?> GetByUserAsync(int userId);
    }
}