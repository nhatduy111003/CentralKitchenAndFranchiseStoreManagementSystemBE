using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class SupportRequest
    {
        public Guid SupportRequestId { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public string Message { get; set; } = null!;
        public string Status { get; set; } = "OPEN";
        public DateTime CreatedAt { get; set; }
    }

}
