using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DTO.Requests.Inventory
{
    public class InventoryHistoryQuery
    {
        public string? Q { get; set; }

        public string? Type { get; set; } // IN, OUT, ADJUST, WASTE, ALL

        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }

        public int? IngredientId { get; set; }
        public int? ProductId { get; set; }

        public string? SortBy { get; set; } = "createdAt"; // createdAt, quantity, type
        public string? SortDir { get; set; } = "desc";    // asc, desc

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
