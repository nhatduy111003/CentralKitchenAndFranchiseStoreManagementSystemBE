namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class AllocationItem
    {
        public int AllocationItemId { get; set; }
        public int AllocationId { get; set; }

        public int FranchiseId { get; set; }
        public Franchise Franchise { get; set; } = null!;

        public int? CentralKitchenId { get; set; }
        public CentralKitchen? CentralKitchen { get; set; }

        public int ProductId { get; set; }
        public decimal Quantity { get; set; }

        public Allocation Allocation { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }
}