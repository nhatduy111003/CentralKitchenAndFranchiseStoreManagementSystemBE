using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class Permission
    {
        public int PermissionId { get; set; }

        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;

        public string GroupName { get; set; } = null!;
        public string Description { get; set; } = null!;

        public ICollection<RolePermission> RolePermissions { get; set; }
            = new List<RolePermission>();
    }

}
