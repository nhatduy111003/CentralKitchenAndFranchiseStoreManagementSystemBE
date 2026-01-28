using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class Franchise
    {
        public Guid FranchiseId { get; set; }

        public string Name { get; set; } = null!;
        public string Type { get; set; } = null!; // STORE, CENTRAL_KITCHEN
        public string Status { get; set; } = "ACTIVE";

        public string Address { get; set; } = null!;
        public string? Location { get; set; }

        public ICollection<UserFranchise> UserFranchises { get; set; } = new List<UserFranchise>();
    }



}
