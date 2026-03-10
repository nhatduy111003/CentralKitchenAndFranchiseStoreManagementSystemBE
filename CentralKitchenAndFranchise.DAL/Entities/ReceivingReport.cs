using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class ReceivingReport
    {
        public int ReceivingReportId { get; set; }
        public int DeliveryId { get; set; }
        public DateTime ReceivedAt { get; set; }

        public Delivery Delivery { get; set; } = null!;
    }


}
