using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class UserFranchise
    {
        public int UserId { get; set; }
        public int FranchiseId { get; set; }
        public DateTime AssignedAt { get; set; }

        public User User { get; set; } = null!;
        public Franchise Franchise { get; set; } = null!;
    }

}
