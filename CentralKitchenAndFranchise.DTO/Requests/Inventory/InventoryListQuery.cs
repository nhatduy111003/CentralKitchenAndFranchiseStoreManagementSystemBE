using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests.Inventory
{
    public class InventoryListQuery
    {
        public string? Q { get; set; }
        public bool? OnlyPositive { get; set; } = true;
        public bool? NearExpiryOnly { get; set; } = false;

        public DateOnly? ExpireFrom { get; set; }
        public DateOnly? ExpireTo { get; set; }

        public string? SortBy { get; set; } = "name";   // name, quantity, expiry
        public string? SortDir { get; set; } = "asc";   // asc, desc

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
