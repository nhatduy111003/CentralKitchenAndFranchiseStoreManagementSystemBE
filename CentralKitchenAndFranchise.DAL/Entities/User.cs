using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class User
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }

        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string Status { get; set; } = "ACTIVE";

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public Role Role { get; set; } = null!;
        public ICollection<UserFranchise> UserFranchises { get; set; } = new List<UserFranchise>();
    }
}
