using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests
{
    public class CreateUserRequest
    {
        public string Status { get; set; }
        public List<string> Roles { get; set; }
        public List<Guid> OrganizationalUnitIds { get; set; }
    }

}
