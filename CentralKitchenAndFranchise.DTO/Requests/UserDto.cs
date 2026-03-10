using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests
{
    public class UserDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Status { get; set; } = null!;

        public int RoleId { get; set; }
        public string RoleName { get; set; } = null!;

        public DateTimeOffset CreatedAt { get; set; }
    }
}
