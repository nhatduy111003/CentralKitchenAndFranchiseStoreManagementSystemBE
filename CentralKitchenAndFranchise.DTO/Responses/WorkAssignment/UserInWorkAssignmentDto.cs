using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Responses.WorkAssignment
{
    public class UserInWorkAssignmentDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string RoleName { get; set; } = default!;
        public string AssignmentType { get; set; } = default!;
        public int? FranchiseId { get; set; }
        public int? CentralKitchenId { get; set; }
        public DateTime AssignedAt { get; set; }
    }
}
