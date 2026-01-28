using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class AuditLog
    {
        public Guid AuditLogId { get; set; }

        public Guid ActorId { get; set; }
        public User Actor { get; set; } = null!;

        public string Action { get; set; } = null!;
        public string Entity { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }


}
