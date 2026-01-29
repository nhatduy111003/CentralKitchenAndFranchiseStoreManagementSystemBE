using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests
{
    public class UpdateUserRequestDto
    {
        public int RoleId { get; set; }
        public string Status { get; set; } = null!;
    }

}
