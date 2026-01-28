using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class ReceivingReport
    {
        public Guid ReceivingReportId { get; set; }

        public Guid DeliveryId { get; set; }
        public Delivery Delivery { get; set; } = null!;

        public DateTime ConfirmedAt { get; set; }
    }

}
