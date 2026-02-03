using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests
{
    public class CreatePermissionDto
    {
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string GroupName { get; set; } = null!;
        public string Description { get; set; } = null!;
    }

}
