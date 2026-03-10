using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class SupportRequest
    {
        public int SupportRequestId { get; set; }
        public int UserId { get; set; }
        public string Content { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        public User User { get; set; } = null!;
    }


}
