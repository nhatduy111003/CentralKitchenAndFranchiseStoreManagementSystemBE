namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class DeliveryPlan
    {
        public int DeliveryPlanId { get; set; }
        public int? StoreOrderId { get; set; }

        // destination franchise
        public int FranchiseId { get; set; }
        public Franchise Franchise { get; set; } = null!;

        // source central kitchen
        public int? CentralKitchenId { get; set; }
        public CentralKitchen? CentralKitchen { get; set; }

        public DateOnly PlannedDate { get; set; }
        public StoreOrder? StoreOrder { get; set; }
        public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
    }
}