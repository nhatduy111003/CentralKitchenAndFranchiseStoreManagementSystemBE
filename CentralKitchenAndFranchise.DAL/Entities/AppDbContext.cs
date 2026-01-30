using CentralKitchenAndFranchise.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        // =======================
        // AUTHENTICATE & AUTHORIZE
        // =======================
        public DbSet<RevokedToken> RevokedTokens { get; set; }

        // =======================
        // MASTER
        // =======================
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Franchise> Franchises => Set<Franchise>();
        public DbSet<UserFranchise> UserFranchises => Set<UserFranchise>();

        public DbSet<Ingredient> Ingredients => Set<Ingredient>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Recipe> Recipes => Set<Recipe>();

        public DbSet<Bom> Boms => Set<Bom>();
        public DbSet<BomItem> BomItems => Set<BomItem>();

        // =======================
        // INVENTORY
        // =======================
        public DbSet<IngredientBatch> IngredientBatches => Set<IngredientBatch>();
        public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

        // =======================
        // STORE
        // =======================
        public DbSet<StoreCatalog> StoreCatalogs => Set<StoreCatalog>();
        public DbSet<StoreOrder> StoreOrders => Set<StoreOrder>();
        public DbSet<StoreOrderItem> StoreOrderItems => Set<StoreOrderItem>();

        // =======================
        // DEMAND / ALLOCATION
        // =======================
        public DbSet<DemandAggregation> DemandAggregations => Set<DemandAggregation>();
        public DbSet<DemandItem> DemandItems => Set<DemandItem>();
        public DbSet<Allocation> Allocations => Set<Allocation>();
        public DbSet<AllocationItem> AllocationItems => Set<AllocationItem>();

        // =======================
        // PRODUCTION
        // =======================
        public DbSet<ProductionPlan> ProductionPlans => Set<ProductionPlan>();
        public DbSet<ProductionPlanItem> ProductionPlanItems => Set<ProductionPlanItem>();
        public DbSet<ProductionBatch> ProductionBatches => Set<ProductionBatch>();

        // =======================
        // DELIVERY
        // =======================
        public DbSet<DeliveryPlan> DeliveryPlans => Set<DeliveryPlan>();
        public DbSet<Delivery> Deliveries => Set<Delivery>();
        public DbSet<ReceivingReport> ReceivingReports => Set<ReceivingReport>();

        // =======================
        // SALES / SUPPORT / AUDIT
        // =======================
        public DbSet<SalesRecord> SalesRecords => Set<SalesRecord>();
        public DbSet<SupportRequest> SupportRequests => Set<SupportRequest>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // =======================
            // ROLES
            // =======================
            modelBuilder.Entity<Role>(e =>
            {
                e.ToTable("roles");
                e.HasKey(x => x.RoleId);
                e.HasIndex(x => x.Name).IsUnique();
            });

            // =======================
            // USERS
            // =======================
            modelBuilder.Entity<User>(e =>
            {
                e.ToTable("users");
                e.HasKey(x => x.UserId);

                e.HasIndex(x => x.Username).IsUnique();
                e.HasIndex(x => x.Email).IsUnique();

                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

                e.HasOne(x => x.Role)
                    .WithMany(x => x.Users)
                    .HasForeignKey(x => x.RoleId);
            });

            // =======================
            // FRANCHISES
            // =======================
            modelBuilder.Entity<Franchise>(e =>
            {
                e.ToTable("franchises");
                e.HasKey(x => x.FranchiseId);
            });

            // =======================
            // USER_FRANCHISES (M:N)
            // =======================
            modelBuilder.Entity<UserFranchise>(e =>
            {
                e.ToTable("user_franchises");
                e.HasKey(x => new { x.UserId, x.FranchiseId });

                e.Property(x => x.AssignedAt).HasDefaultValueSql("now()");
            });

            // =======================
            // INGREDIENTS
            // =======================
            modelBuilder.Entity<Ingredient>(e =>
            {
                e.ToTable("ingredients");
                e.HasKey(x => x.IngredientId);
                e.HasIndex(x => x.Name).IsUnique();
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            });

            // =======================
            // SUPPLIERS
            // =======================
            modelBuilder.Entity<Supplier>(e =>
            {
                e.ToTable("suppliers");
                e.HasKey(x => x.SupplierId);
                e.HasIndex(x => x.Name).IsUnique();
            });

            // =======================
            // PRODUCTS
            // =======================
            modelBuilder.Entity<Product>(e =>
            {
                e.ToTable("products");
                e.HasKey(x => x.ProductId);
                e.HasIndex(x => x.Sku).IsUnique();
            });

            // =======================
            // RECIPES
            // =======================
            modelBuilder.Entity<Recipe>(e =>
            {
                e.ToTable("recipes");
                e.HasKey(x => x.RecipeId);

                e.HasIndex(x => new { x.ProductId, x.Version }).IsUnique();
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            });

            // =======================
            // BOMS
            // =======================
            modelBuilder.Entity<Bom>(e =>
            {
                e.ToTable("boms");
                e.HasKey(x => x.BomId);
                e.HasIndex(x => new { x.ProductId, x.Version }).IsUnique();
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<BomItem>(e =>
            {
                e.ToTable("bom_items");
                e.HasKey(x => x.BomItemId);
            });

            // =======================
            // INVENTORY
            // =======================
            modelBuilder.Entity<IngredientBatch>(e =>
            {
                e.ToTable("ingredient_batches");
                e.HasKey(x => x.BatchId);
                e.HasIndex(x => new { x.IngredientId, x.BatchCode, x.FranchiseId }).IsUnique();
            });

            modelBuilder.Entity<InventoryMovement>(e =>
            {
                e.ToTable("inventory_movements");
                e.HasKey(x => x.MovementId);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            });

            // =======================
            // STORE
            // =======================
            modelBuilder.Entity<StoreCatalog>(e =>
            {
                e.ToTable("store_catalog");
                e.HasKey(x => new { x.FranchiseId, x.ProductId });
            });

            modelBuilder.Entity<StoreOrder>(e =>
            {
                e.ToTable("store_orders");
                e.HasKey(x => x.StoreOrderId);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<StoreOrderItem>(e =>
            {
                e.ToTable("store_order_items");
                e.HasKey(x => x.StoreOrderItemId);
            });

            // =======================
            // DEMAND / ALLOCATION
            // =======================
            modelBuilder.Entity<DemandAggregation>(e =>
            {
                e.ToTable("demand_aggregations");
                e.HasKey(x => x.DemandAggregationId);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<DemandItem>(e =>
            {
                e.ToTable("demand_items");
                e.HasKey(x => x.DemandItemId);
            });

            modelBuilder.Entity<Allocation>(e =>
            {
                e.ToTable("allocations");
                e.HasKey(x => x.AllocationId);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<AllocationItem>(e =>
            {
                e.ToTable("allocation_items");
                e.HasKey(x => x.AllocationItemId);
            });

            // =======================
            // PRODUCTION
            // =======================
            modelBuilder.Entity<ProductionPlan>(e =>
            {
                e.ToTable("production_plans");
                e.HasKey(x => x.ProductionPlanId);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<ProductionPlanItem>(e =>
            {
                e.ToTable("production_plan_items");
                e.HasKey(x => x.ProductionPlanItemId);
            });

            modelBuilder.Entity<ProductionBatch>(e =>
            {
                e.ToTable("production_batches");
                e.HasKey(x => x.ProductionBatchId);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            });

            // =======================
            // DELIVERY
            // =======================
            modelBuilder.Entity<DeliveryPlan>(e =>
            {
                e.ToTable("delivery_plans");
                e.HasKey(x => x.DeliveryPlanId);
            });

            modelBuilder.Entity<Delivery>(e =>
            {
                e.ToTable("deliveries");
                e.HasKey(x => x.DeliveryId);
                e.Property(x => x.DeliveredAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<ReceivingReport>(e =>
            {
                e.ToTable("receiving_reports");
                e.HasKey(x => x.ReceivingReportId);
                e.Property(x => x.ReceivedAt).HasDefaultValueSql("now()");
            });

            // =======================
            // SALES / SUPPORT / AUDIT
            // =======================
            modelBuilder.Entity<SalesRecord>(e =>
            {
                e.ToTable("sales_records");
                e.HasKey(x => x.SalesRecordId);
            });

            modelBuilder.Entity<SupportRequest>(e =>
            {
                e.ToTable("support_requests");
                e.HasKey(x => x.SupportRequestId);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<AuditLog>(e =>
            {
                e.ToTable("audit_logs");
                e.HasKey(x => x.AuditLogId);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            });

            // =======================
            // AUTHENTICATE & AUTHORIZE
            // =======================

            modelBuilder.Entity<RevokedToken>(e =>
            {
                e.ToTable("revoked_tokens");
                e.HasKey(x => x.Id);
                e.Property(x => x.Jti).IsRequired();
                e.HasIndex(x => x.Jti).IsUnique();
            });

        }
    }
}
