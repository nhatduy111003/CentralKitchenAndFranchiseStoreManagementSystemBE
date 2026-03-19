using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class StoreOrderHistory
    {
        public int StoreOrderHistoryId { get; set; }
        public int StoreOrderId { get; set; }

        public string ActionType { get; set; } = default!;
        public string ActionLabel { get; set; } = default!;

        public string? OldStatus { get; set; }
        public string? NewStatus { get; set; }

        public string? Note { get; set; }

        public int? PerformedByUserId { get; set; }
        public DateTime PerformedAt { get; set; }

        public StoreOrder StoreOrder { get; set; } = default!;
    }
}
