using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests
{
    public class UpdateUserDto
    {
        public string Username { get; set; } = null!;
        public string Status { get; set; } = "ACTIVE";
        public Guid RoleId { get; set; }
    }


}
