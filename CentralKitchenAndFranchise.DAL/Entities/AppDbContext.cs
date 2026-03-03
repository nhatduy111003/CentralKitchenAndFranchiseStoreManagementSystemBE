using CentralKitchenAndFranchise.DAL.Enums;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public override int SaveChanges()
        {
            ApplyTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Apply timestamps for common columns:
        /// - CreatedAt (DateTime/DateTime?)
        /// - UpdatedAt (DateTime/DateTime?)
        /// - UpdateAt  (DateTime/DateTime?)  // legacy typo in ProductionPlan
        ///
        /// Behavior:
        /// - Added: set CreatedAt (if exists) + UpdatedAt/UpdateAt (if exists)
        /// - Modified: do NOT modify CreatedAt; set UpdatedAt/UpdateAt
        /// </summary>
        private void ApplyTimestamps()
        {
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
                    continue;

                // CreatedAt
                if (HasDateTimeProperty(entry, "CreatedAt"))
                {
                    if (entry.State == EntityState.Added)
                        SetDateTime(entry, "CreatedAt", now);
                    else
                        entry.Property("CreatedAt").IsModified = false;
                }

                // UpdatedAt
                if (HasDateTimeProperty(entry, "UpdatedAt"))
                {
                    if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                        SetDateTime(entry, "UpdatedAt", now);
                }

                // UpdateAt (legacy)
                if (HasDateTimeProperty(entry, "UpdateAt"))
                {
                    if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                        SetDateTime(entry, "UpdateAt", now);
                }
            }
        }

        private static bool HasDateTimeProperty(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string propertyName)
        {
            var prop = entry.Metadata.FindProperty(propertyName);
            if (prop is null) return false;

            return prop.ClrType == typeof(DateTime) || prop.ClrType == typeof(DateTime?);
        }

        private static void SetDateTime(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string propertyName, DateTime value)
        {
            var prop = entry.Metadata.FindProperty(propertyName);
            if (prop is null) return;

            if (prop.ClrType != typeof(DateTime) && prop.ClrType != typeof(DateTime?))
                return;

            entry.Property(propertyName).CurrentValue = value;
        }

        // ===== DbSets =====
        public DbSet<RevokedToken> RevokedTokens { get; set; }

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

        public DbSet<IngredientBatch> IngredientBatches => Set<IngredientBatch>();
        public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
        public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
        public DbSet<ProductMovement> ProductMovements => Set<ProductMovement>();

        public DbSet<StoreCatalog> StoreCatalogs => Set<StoreCatalog>();
        public DbSet<StoreOrder> StoreOrders => Set<StoreOrder>();
        public DbSet<StoreOrderItem> StoreOrderItems => Set<StoreOrderItem>();

        public DbSet<DemandAggregation> DemandAggregations => Set<DemandAggregation>();
        public DbSet<DemandItem> DemandItems => Set<DemandItem>();
        public DbSet<Allocation> Allocations => Set<Allocation>();
        public DbSet<AllocationItem> AllocationItems => Set<AllocationItem>();

        public DbSet<ProductionPlan> ProductionPlans => Set<ProductionPlan>();
        public DbSet<ProductionPlanItem> ProductionPlanItems => Set<ProductionPlanItem>();
        public DbSet<ProductionBatch> ProductionBatches => Set<ProductionBatch>();

        public DbSet<DeliveryPlan> DeliveryPlans => Set<DeliveryPlan>();
        public DbSet<Delivery> Deliveries => Set<Delivery>();
        public DbSet<DeliveryProductItem> DeliveryProductItems => Set<DeliveryProductItem>();
        public DbSet<DeliveryIngredientItem> DeliveryIngredientItems => Set<DeliveryIngredientItem>();
        public DbSet<ReceivingReport> ReceivingReports => Set<ReceivingReport>();

        public DbSet<SalesRecord> SalesRecords => Set<SalesRecord>();
        public DbSet<SupportRequest> SupportRequests => Set<SupportRequest>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ===== RBAC / Auth =====
            modelBuilder.Entity<Role>(e =>
            {
                e.ToTable("roles");
                e.HasKey(x => x.RoleId);
                e.HasIndex(x => x.Name).IsUnique();

                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("ACTIVE").IsRequired();

                e.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<Permission>(e =>
            {
                e.ToTable("permissions");
                e.HasKey(x => x.PermissionId);
                e.HasIndex(x => x.Code).IsUnique();

                e.Property(x => x.Code).HasMaxLength(100).IsRequired();
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
                e.Property(x => x.GroupName).HasMaxLength(200).IsRequired();
                e.Property(x => x.Description).HasColumnType("text");

                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("ACTIVE").IsRequired();
                e.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
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

            modelBuilder.Entity<User>(e =>
            {
                e.ToTable("users");
                e.HasKey(x => x.UserId);

                e.HasIndex(x => x.Username).IsUnique();
                e.HasIndex(x => x.Email).IsUnique();

                e.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

                e.HasOne(x => x.Role)
                    .WithMany(x => x.Users)
                    .HasForeignKey(x => x.RoleId);
            });

            modelBuilder.Entity<RevokedToken>(e =>
            {
                e.ToTable("RevokedTokens");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
                e.Property(x => x.Jti).IsRequired();
            });

            // ===== Franchise / UserFranchise =====
            modelBuilder.Entity<Franchise>(e =>
            {
                e.ToTable("franchises");
                e.HasKey(x => x.FranchiseId);

                e.Property(x => x.CreatedAt)
                    .HasColumnType("timestamptz")
                    .HasDefaultValueSql("now()")
                    .IsRequired();

                e.Property(x => x.UpdatedAt)
                    .HasColumnType("timestamptz")
                    .HasDefaultValueSql("now()")
                    .IsRequired();
            });

            modelBuilder.Entity<UserFranchise>(e =>
            {
                e.ToTable("user_franchises");
                e.HasKey(x => new { x.UserId, x.FranchiseId });

                // enforce single franchise per user (per migration)
                e.HasIndex(x => x.UserId).IsUnique();

                e.Property(x => x.AssignedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
            });

            // ===== Master data =====
            modelBuilder.Entity<Ingredient>(e =>
            {
                e.ToTable("ingredients");
                e.HasKey(x => x.IngredientId);

                e.Property(x => x.Status).HasDefaultValue("ACTIVE");
                e.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<Supplier>(e =>
            {
                e.ToTable("suppliers");
                e.HasKey(x => x.SupplierId);

                e.HasIndex(x => x.Name).IsUnique();

                e.Property(x => x.Status).HasDefaultValue("ACTIVE");
            });

            modelBuilder.Entity<Product>(e =>
            {
                e.ToTable("products");
                e.HasKey(x => x.ProductId);
                e.Property(x => x.Status).HasDefaultValue("ACTIVE");
            });

            // ===== Store Catalog =====
            modelBuilder.Entity<StoreCatalog>(e =>
            {
                e.ToTable("store_catalogs");
                e.HasKey(x => new { x.FranchiseId, x.ProductId });

                e.Property(x => x.Status).HasDefaultValue("ACTIVE");
                e.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

                e.HasOne(x => x.Franchise)
                    .WithMany(f => f.StoreCatalogs)
                    .HasForeignKey(x => x.FranchiseId);

                e.HasOne(x => x.Product)
                    .WithMany(p => p.StoreCatalogs)
                    .HasForeignKey(x => x.ProductId);
            });

            // ===== Recipe / BOM =====
            modelBuilder.Entity<Recipe>(e =>
            {
                e.ToTable("recipes");
                e.HasKey(x => x.RecipeId);

                e.HasIndex(x => new { x.ProductId, x.Version }).IsUnique();

                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("DRAFT").IsRequired();
                e.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
                e.Property(x => x.Instructions).HasColumnType("text");

                e.HasOne(x => x.Product)
                    .WithMany()
                    .HasForeignKey(x => x.ProductId);
            });

            modelBuilder.Entity<Bom>(e =>
            {
                e.ToTable("boms");
                e.HasKey(x => x.BomId);

                e.HasIndex(x => new { x.ProductId, x.Version }).IsUnique();

                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("DRAFT").IsRequired();
                e.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

                e.HasOne(x => x.Product)
                    .WithMany()
                    .HasForeignKey(x => x.ProductId);
            });

            modelBuilder.Entity<BomItem>(e =>
            {
                e.ToTable("bom_items");
                e.HasKey(x => x.BomItemId);

                e.HasOne(x => x.Bom)
                    .WithMany(b => b.Items)
                    .HasForeignKey(x => x.BomId);

                e.HasOne(x => x.Ingredient)
                    .WithMany()
                    .HasForeignKey(x => x.IngredientId);
            });

            // ===== Inventory (Ingredient) =====
            modelBuilder.Entity<IngredientBatch>(e =>
            {
                e.ToTable("ingredient_batches");
                e.HasKey(x => x.BatchId);

                e.HasIndex(x => new { x.IngredientId, x.BatchCode, x.FranchiseId }).IsUnique();

                e.HasOne(x => x.Ingredient)
                    .WithMany()
                    .HasForeignKey(x => x.IngredientId);

                e.HasOne(x => x.Franchise)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseId);
            });

            modelBuilder.Entity<InventoryMovement>(e =>
            {
                e.ToTable("inventory_movements");
                e.HasKey(x => x.MovementId);

                e.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

                e.HasOne(x => x.Batch)
                    .WithMany(b => b.InventoryMovements)
                    .HasForeignKey(x => x.BatchId);
            });

            // ===== Inventory (Product) =====
            modelBuilder.Entity<ProductBatch>(e =>
            {
                e.ToTable("product_batches");
                e.HasKey(x => x.BatchId);

                e.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

                e.HasOne(x => x.Product)
                    .WithMany()
                    .HasForeignKey(x => x.ProductId);

                e.HasOne(x => x.Franchise)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseId);
            });

            modelBuilder.Entity<ProductMovement>(e =>
            {
                e.ToTable("product_movements");
                e.HasKey(x => x.MovementId);

                e.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");

                e.HasIndex(x => x.DeliveryId);

                e.HasOne(x => x.Batch)
                    .WithMany(b => b.ProductMovements)
                    .HasForeignKey(x => x.BatchId);
            });

            // ===== Store Orders =====
            modelBuilder.Entity<StoreOrder>(e =>
            {
                e.ToTable("StoreOrders");
                e.HasKey(x => x.StoreOrderId);

                e.HasIndex(x => x.FranchiseId);

                e.HasOne(x => x.Franchise)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseId);
            });

            modelBuilder.Entity<StoreOrderItem>(e =>
            {
                e.ToTable("StoreOrderItems");
                e.HasKey(x => x.StoreOrderItemId);

                e.HasIndex(x => x.StoreOrderId);
                e.HasIndex(x => x.ProductId);

                e.HasOne(x => x.StoreOrder)
                    .WithMany(o => o.Items)
                    .HasForeignKey(x => x.StoreOrderId);

                e.HasOne(x => x.Product)
                    .WithMany()
                    .HasForeignKey(x => x.ProductId);
            });

            // ===== Demand / Allocation =====
            modelBuilder.Entity<DemandAggregation>(e =>
            {
                e.ToTable("DemandAggregations");
                e.HasKey(x => x.DemandAggregationId);
            });

            modelBuilder.Entity<DemandItem>(e =>
            {
                e.ToTable("DemandItems");
                e.HasKey(x => x.DemandItemId);

                e.HasIndex(x => x.DemandAggregationId);
                e.HasIndex(x => x.ProductId);

                e.HasOne(x => x.DemandAggregation)
                    .WithMany(d => d.DemandItems)
                    .HasForeignKey(x => x.DemandAggregationId);

                e.HasOne(x => x.Product)
                    .WithMany()
                    .HasForeignKey(x => x.ProductId);
            });

            modelBuilder.Entity<Allocation>(e =>
            {
                e.ToTable("Allocations");
                e.HasKey(x => x.AllocationId);

                e.HasIndex(x => x.DemandAggregationId);

                e.HasOne(x => x.DemandAggregation)
                    .WithMany()
                    .HasForeignKey(x => x.DemandAggregationId);
            });

            modelBuilder.Entity<AllocationItem>(e =>
            {
                e.ToTable("AllocationItems");
                e.HasKey(x => x.AllocationItemId);

                e.HasIndex(x => x.AllocationId);
                e.HasIndex(x => x.FranchiseId);
                e.HasIndex(x => x.ProductId);

                e.HasOne(x => x.Allocation)
                    .WithMany(a => a.AllocationItems)
                    .HasForeignKey(x => x.AllocationId);

                e.HasOne(x => x.Franchise)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseId);

                e.HasOne(x => x.Product)
                    .WithMany()
                    .HasForeignKey(x => x.ProductId);
            });

            // ===== Production =====
            modelBuilder.Entity<ProductionPlan>(e =>
            {
                e.ToTable("ProductionPlans");
                e.HasKey(x => x.ProductionPlanId);

                e.HasIndex(x => x.FranchiseId);

                // IMPORTANT: keep enum storage per migrations (int)
                e.Property(x => x.Status);

                e.HasOne(x => x.Franchise)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseId);
            });

            modelBuilder.Entity<ProductionPlanItem>(e =>
            {
                e.ToTable("ProductionPlanItems");
                e.HasKey(x => x.ProductionPlanItemId);

                e.HasIndex(x => x.ProductionPlanId);
                e.HasIndex(x => x.ProductId);

                e.HasOne(x => x.ProductionPlan)
                    .WithMany(p => p.Items)
                    .HasForeignKey(x => x.ProductionPlanId);

                e.HasOne(x => x.Product)
                    .WithMany()
                    .HasForeignKey(x => x.ProductId);
            });

            modelBuilder.Entity<ProductionBatch>(e =>
            {
                e.ToTable("ProductionBatches");
                e.HasKey(x => x.ProductionBatchId);

                e.HasIndex(x => x.ProductionPlanId);

                e.HasOne(x => x.ProductionPlan)
                    .WithMany(p => p.ProductionBatches)
                    .HasForeignKey(x => x.ProductionPlanId);
            });

            // ===== Delivery =====
            modelBuilder.Entity<DeliveryPlan>(e =>
            {
                e.ToTable("DeliveryPlans");
                e.HasKey(x => x.DeliveryPlanId);

                e.HasIndex(x => x.FranchiseId);

                e.HasOne(x => x.Franchise)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseId);
            });

            modelBuilder.Entity<Delivery>(e =>
            {
                e.ToTable("Deliveries");
                e.HasKey(x => x.DeliveryId);

                e.HasIndex(x => x.DeliveryPlanId);
                e.HasIndex(x => x.FromFranchiseId);

                e.HasOne(x => x.DeliveryPlan)
                    .WithMany(p => p.Deliveries)
                    .HasForeignKey(x => x.DeliveryPlanId);

                e.HasOne(x => x.FromFranchise)
                    .WithMany()
                    .HasForeignKey(x => x.FromFranchiseId);
            });

            modelBuilder.Entity<DeliveryProductItem>(e =>
            {
                e.ToTable("DeliveryProductItems");
                e.HasKey(x => x.DeliveryProductItemId);

                e.HasIndex(x => x.DeliveryId);
                e.HasIndex(x => x.ProductId);

                e.HasOne(x => x.Delivery)
                    .WithMany(d => d.ProductItems)
                    .HasForeignKey(x => x.DeliveryId);

                e.HasOne(x => x.Product)
                    .WithMany()
                    .HasForeignKey(x => x.ProductId);
            });

            modelBuilder.Entity<DeliveryIngredientItem>(e =>
            {
                e.ToTable("DeliveryIngredientItems");
                e.HasKey(x => x.DeliveryIngredientItemId);

                e.HasIndex(x => x.DeliveryId);
                e.HasIndex(x => x.IngredientId);

                e.HasOne(x => x.Delivery)
                    .WithMany(d => d.IngredientItems)
                    .HasForeignKey(x => x.DeliveryId);

                e.HasOne(x => x.Ingredient)
                    .WithMany()
                    .HasForeignKey(x => x.IngredientId);
            });

            modelBuilder.Entity<ReceivingReport>(e =>
            {
                e.ToTable("ReceivingReports");
                e.HasKey(x => x.ReceivingReportId);

                e.HasIndex(x => x.DeliveryId);

                e.HasOne(x => x.Delivery)
                    .WithMany(d => d.ReceivingReports)
                    .HasForeignKey(x => x.DeliveryId);
            });

            // ===== Sales =====
            modelBuilder.Entity<SalesRecord>(e =>
            {
                e.ToTable("SalesRecords");
                e.HasKey(x => x.SalesRecordId);

                e.HasIndex(x => x.FranchiseId);
                e.HasIndex(x => x.ProductId);

                e.HasOne(x => x.Franchise)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseId);

                e.HasOne(x => x.Product)
                    .WithMany()
                    .HasForeignKey(x => x.ProductId);
            });

            // ===== Support =====
            modelBuilder.Entity<SupportRequest>(e =>
            {
                e.ToTable("SupportRequests");
                e.HasKey(x => x.SupportRequestId);

                e.HasIndex(x => x.UserId);

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId);
            });

            // ===== Audit / Settings =====
            modelBuilder.Entity<AuditLog>(e =>
            {
                e.ToTable("AuditLogs");
                e.HasKey(x => x.AuditLogId);

                e.HasIndex(x => x.UserId);
                e.HasIndex(x => x.FranchiseId);
            });

            modelBuilder.Entity<SystemSetting>(e =>
            {
                e.ToTable("system_settings"); 
                e.HasKey(x => x.SystemSettingId);

                e.HasIndex(x => x.Key).IsUnique();

                e.Property(x => x.Key).HasMaxLength(100).IsRequired();
                e.Property(x => x.Value).HasMaxLength(200).IsRequired();
                e.Property(x => x.Description).HasMaxLength(500);

                e.Property(x => x.CreatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasColumnType("timestamptz").HasDefaultValueSql("now()");
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}