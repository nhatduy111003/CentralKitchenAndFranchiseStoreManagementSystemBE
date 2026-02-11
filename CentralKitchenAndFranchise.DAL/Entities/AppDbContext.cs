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
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

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
        public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
        public DbSet<ProductMovement> ProductMovements => Set<ProductMovement>();

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
        public DbSet<DeliveryProductItem> DeliveryProductItems => Set<DeliveryProductItem>();
        public DbSet<DeliveryIngredientItem> DeliveryIngredientItems => Set<DeliveryIngredientItem>();
        public DbSet<ReceivingReport> ReceivingReports => Set<ReceivingReport>();

        // =======================
        // SALES / SUPPORT / AUDIT
        // =======================
        public DbSet<SalesRecord> SalesRecords => Set<SalesRecord>();
        public DbSet<SupportRequest> SupportRequests => Set<SupportRequest>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

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

                e.Property(x => x.Price)
                    .HasPrecision(18, 2)
                    .HasDefaultValue(0);

                e.Property(x => x.SafetyStock).HasDefaultValue(0);
                e.Property(x => x.WasteThreshold).HasDefaultValue(0);

                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
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

                e.Property(x => x.ProductType).HasDefaultValue("FINISHED");
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
                e.HasIndex(x => x.DeliveryId);

                e.HasOne(x => x.Batch)
                    .WithMany(b => b.InventoryMovements)
                    .HasForeignKey(x => x.BatchId);
            });

            modelBuilder.Entity<ProductBatch>(e =>
            {
                e.ToTable("product_batches");
                e.HasKey(x => x.BatchId);
                e.HasIndex(x => new { x.ProductId, x.BatchCode, x.FranchiseId }).IsUnique();
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<ProductMovement>(e =>
            {
                e.ToTable("product_movements");
                e.HasKey(x => x.MovementId);

                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                e.HasIndex(x => x.DeliveryId);

                e.HasOne(x => x.Batch)
                    .WithMany(b => b.ProductMovements)
                    .HasForeignKey(x => x.BatchId);
            });

            // =======================
            // STORE
            // =======================
            modelBuilder.Entity<StoreCatalog>(e =>
            {
                e.ToTable("store_catalog");
                e.HasKey(x => new { x.FranchiseId, x.ProductId });

                e.Property(x => x.Price).HasDefaultValue(0);
                e.Property(x => x.Status).HasDefaultValue("ACTIVE");
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

                e.HasOne(x => x.Franchise)
                    .WithMany(f => f.StoreCatalogs)
                    .HasForeignKey(x => x.FranchiseId);

                e.HasOne(x => x.Product)
                    .WithMany(p => p.StoreCatalogs)
                    .HasForeignKey(x => x.ProductId);
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

                e.HasOne(x => x.Franchise)
                    .WithMany(f => f.DeliveryPlans)
                    .HasForeignKey(x => x.FranchiseId);
            });

            modelBuilder.Entity<Delivery>(e =>
            {
                e.ToTable("deliveries");
                e.HasKey(x => x.DeliveryId);

                e.Property(x => x.Status).HasDefaultValue("CREATED");
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                e.Property(x => x.DeliveredAt).HasDefaultValueSql("now()");

                e.HasOne(x => x.DeliveryPlan)
                    .WithMany(p => p.Deliveries)
                    .HasForeignKey(x => x.DeliveryPlanId);

                e.HasOne(x => x.FromFranchise)
                    .WithMany()
                    .HasForeignKey(x => x.FromFranchiseId);
            });

            modelBuilder.Entity<DeliveryProductItem>(e =>
            {
                e.ToTable("delivery_product_items");
                e.HasKey(x => x.DeliveryProductItemId);
                e.HasIndex(x => new { x.DeliveryId, x.ProductId }).IsUnique();

                e.HasOne(x => x.Delivery)
                    .WithMany(d => d.ProductItems)
                    .HasForeignKey(x => x.DeliveryId);

                e.HasOne(x => x.Product)
                    .WithMany(p => p.DeliveryProductItems)
                    .HasForeignKey(x => x.ProductId);
            });

            modelBuilder.Entity<DeliveryIngredientItem>(e =>
            {
                e.ToTable("delivery_ingredient_items");
                e.HasKey(x => x.DeliveryIngredientItemId);
                e.HasIndex(x => new { x.DeliveryId, x.IngredientId }).IsUnique();

                e.HasOne(x => x.Delivery)
                    .WithMany(d => d.IngredientItems)
                    .HasForeignKey(x => x.DeliveryId);

                e.HasOne(x => x.Ingredient)
                    .WithMany()
                    .HasForeignKey(x => x.IngredientId);
            });

            modelBuilder.Entity<ReceivingReport>(e =>
            {
                e.ToTable("receiving_reports");
                e.HasKey(x => x.ReceivingReportId);
                e.Property(x => x.ReceivedAt).HasDefaultValueSql("now()");

                e.HasOne(x => x.Delivery)
                    .WithMany(d => d.ReceivingReports)
                    .HasForeignKey(x => x.DeliveryId);
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
                e.HasIndex(x => x.Action);

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId);

                e.HasOne(x => x.Franchise)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseId);
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

            // =======================
            // SYSTEM SETTINGS
            // =======================
            modelBuilder.Entity<SystemSetting>(e =>
            {
                e.ToTable("system_settings");
                e.HasKey(x => x.SystemSettingId);
                e.HasIndex(x => x.Key).IsUnique();
                e.Property(x => x.Key).HasMaxLength(100).IsRequired();
                e.Property(x => x.Value).HasMaxLength(200).IsRequired();
                e.Property(x => x.Description).HasMaxLength(500);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            });
            modelBuilder.Entity<Permission>(e =>
            {
                e.ToTable("permissions");

                e.HasKey(x => x.PermissionId);

                e.Property(x => x.Code)
                    .IsRequired()
                    .HasMaxLength(100);

                e.HasIndex(x => x.Code).IsUnique();

                e.Property(x => x.Name).IsRequired();
                e.Property(x => x.GroupName).IsRequired();
                e.Property(x => x.Description).IsRequired();
            });
            modelBuilder.Entity<RolePermission>(e =>
            {
                e.ToTable("role_permissions");

                e.HasKey(x => new { x.RoleId, x.PermissionId });

                e.HasOne(x => x.Role)
                    .WithMany(r => r.RolePermissions)
                    .HasForeignKey(x => x.RoleId);

                e.HasOne(x => x.Permission)
                    .WithMany(p => p.RolePermissions)
                    .HasForeignKey(x => x.PermissionId);
            });


        }

    }
}
