using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class UserFranchise
    {
        public Guid UserId { get; set; }
        public Guid FranchiseId { get; set; }  

        public string Role { get; set; } = null!;
        public string Status { get; set; } = null!;
    }


}
