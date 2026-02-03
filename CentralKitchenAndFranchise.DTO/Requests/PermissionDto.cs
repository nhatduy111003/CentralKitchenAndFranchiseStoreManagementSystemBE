using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests
{
    public class PermissionDto
    {
        public int PermissionId { get; set; }
        public string Code { get; set; } = null!;
        public string Description { get; set; } = null!;
    }

}
