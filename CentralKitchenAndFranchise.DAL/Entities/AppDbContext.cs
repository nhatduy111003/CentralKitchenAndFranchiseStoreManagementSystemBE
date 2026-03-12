using CentralKitchenAndFranchise.DAL.Enums;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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
        /// - UpdateAt  (DateTime/DateTime?) // legacy typo (ProductionPlan)
        /// </summary>
        private void ApplyTimestamps()
        {
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
                    continue;

                if (HasDateTimeProperty(entry, "CreatedAt"))
                {
                    if (entry.State == EntityState.Added)
                        SetDateTime(entry, "CreatedAt", now);
                    else
                        entry.Property("CreatedAt").IsModified = false;
                }

                if (HasDateTimeProperty(entry, "UpdatedAt"))
                    SetDateTime(entry, "UpdatedAt", now);

                if (HasDateTimeProperty(entry, "UpdateAt"))
                    SetDateTime(entry, "UpdateAt", now);
            }
        }

        private static bool HasDateTimeProperty(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string propertyName)
        {
            var prop = entry.Metadata.FindProperty(propertyName);
            return prop != null && (prop.ClrType == typeof(DateTime) || prop.ClrType == typeof(DateTime?));
        }

        private static void SetDateTime(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string propertyName, DateTime value)
        {
            var prop = entry.Metadata.FindProperty(propertyName);
            if (prop == null) return;

            if (prop.ClrType != typeof(DateTime) && prop.ClrType != typeof(DateTime?))
                return;

            entry.Property(propertyName).CurrentValue = value;
        }

        // ===== DbSets =====
        public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();

        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

        public DbSet<User> Users => Set<User>();
        public DbSet<Franchise> Franchises => Set<Franchise>();
        public DbSet<CentralKitchen> CentralKitchens => Set<CentralKitchen>();
        public DbSet<UserWorkAssignment> UserWorkAssignments => Set<UserWorkAssignment>();

        public DbSet<Ingredient> Ingredients => Set<Ingredient>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Product> Products => Set<Product>();

        public DbSet<StoreCatalog> StoreCatalogs => Set<StoreCatalog>();
        public DbSet<StoreOrder> StoreOrders => Set<StoreOrder>();
        public DbSet<StoreOrderItem> StoreOrderItems => Set<StoreOrderItem>();

        public DbSet<DemandAggregation> DemandAggregations => Set<DemandAggregation>();
        public DbSet<DemandItem> DemandItems => Set<DemandItem>();
        public DbSet<Allocation> Allocations => Set<Allocation>();
        public DbSet<AllocationItem> AllocationItems => Set<AllocationItem>();

        public DbSet<ProductionPlan> ProductionPlans => Set<ProductionPlan>();
        public DbSet<ProductionPlanItem> ProductionPlanItems => Set<ProductionPlanItem>();
        public DbSet<ProductionRun> ProductionRuns => Set<ProductionRun>();

        public DbSet<Recipe> Recipes => Set<Recipe>();
        public DbSet<Bom> Boms => Set<Bom>();
        public DbSet<BomItem> BomItems => Set<BomItem>();

        public DbSet<IngredientBatch> IngredientBatches => Set<IngredientBatch>();
        public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
        public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
        public DbSet<ProductMovement> ProductMovements => Set<ProductMovement>();

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
            // ===== snake_case mapping for ALL tables =====

            modelBuilder.Entity<Role>(e => { e.ToTable("roles"); e.HasKey(x => x.RoleId); });
            modelBuilder.Entity<Permission>(e => { e.ToTable("permissions"); e.HasKey(x => x.PermissionId); });
            modelBuilder.Entity<RolePermission>(e => { e.ToTable("role_permissions"); e.HasKey(x => new { x.RoleId, x.PermissionId }); });

            modelBuilder.Entity<User>(e => { e.ToTable("users"); e.HasKey(x => x.UserId); });

            modelBuilder.Entity<CentralKitchen>(e =>
            {
                e.ToTable("central_kitchens");
                e.HasKey(x => x.CentralKitchenId);
            });

            modelBuilder.Entity<Franchise>(e =>
            {
                e.ToTable("franchises");
                e.HasKey(x => x.FranchiseId);

                e.HasOne(f => f.CentralKitchen)
                    .WithMany(c => c.Franchises)
                    .HasForeignKey(f => f.CentralKitchenId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<UserWorkAssignment>(e =>
            {
                e.ToTable("user_work_assignments");
                e.HasKey(x => x.UserWorkAssignmentId);

                e.Property(x => x.AssignmentType)
                    .HasMaxLength(50);

                e.HasOne(x => x.User)
                    .WithMany(x => x.WorkAssignments)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Franchise)
                    .WithMany(x => x.WorkAssignments)
                    .HasForeignKey(x => x.FranchiseId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.CentralKitchen)
                    .WithMany(x => x.WorkAssignments)
                    .HasForeignKey(x => x.CentralKitchenId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasCheckConstraint("CK_user_work_assignments_owner", @"
    (
        (""AssignmentType"" = 'FRANCHISE' AND ""FranchiseId"" IS NOT NULL AND ""CentralKitchenId"" IS NULL)
        OR
        (""AssignmentType"" = 'CENTRAL_KITCHEN' AND ""FranchiseId"" IS NULL AND ""CentralKitchenId"" IS NOT NULL)
    )");
            });
            modelBuilder.Entity<Ingredient>(e =>
            {
                e.HasOne(x => x.Supplier)
                    .WithMany(x => x.Ingredients)
                    .HasForeignKey(x => x.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Supplier>(e => { e.ToTable("suppliers"); e.HasKey(x => x.SupplierId); });
            modelBuilder.Entity<Product>(e => { e.ToTable("products"); e.HasKey(x => x.ProductId); });

            modelBuilder.Entity<StoreCatalog>(e =>
            {
                e.ToTable("store_catalog");
                e.HasKey(x => new { x.FranchiseId, x.ProductId });
            });

            // store orders
            modelBuilder.Entity<StoreOrder>(e => { e.ToTable("store_orders"); e.HasKey(x => x.StoreOrderId); });
            modelBuilder.Entity<StoreOrderItem>(e => { e.ToTable("store_order_items"); e.HasKey(x => x.StoreOrderItemId); });

            // demand / allocation
            modelBuilder.Entity<DemandAggregation>(e => { e.ToTable("demand_aggregations"); e.HasKey(x => x.DemandAggregationId); });
            modelBuilder.Entity<DemandItem>(e => { e.ToTable("demand_items"); e.HasKey(x => x.DemandItemId); });

            modelBuilder.Entity<Allocation>(e => { e.ToTable("allocations"); e.HasKey(x => x.AllocationId); });
            modelBuilder.Entity<AllocationItem>(e =>
            {
                e.ToTable("allocation_items");
                e.HasKey(x => x.AllocationItemId);

                e.HasOne(x => x.Allocation)
                    .WithMany(x => x.AllocationItems)
                    .HasForeignKey(x => x.AllocationId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Franchise)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Product)
                    .WithMany(x => x.AllocationItems)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            // production
            modelBuilder.Entity<ProductionPlan>(e =>
            {
                e.ToTable("production_plans");
                e.HasKey(x => x.ProductionPlanId);

                e.HasOne(x => x.CentralKitchen)
                    .WithMany(x => x.ProductionPlans)
                    .HasForeignKey(x => x.CentralKitchenId)
                    .OnDelete(DeleteBehavior.Restrict);
            }); modelBuilder.Entity<ProductionPlanItem>(e => { e.ToTable("production_plan_items"); e.HasKey(x => x.ProductionPlanItemId); });

            modelBuilder.Entity<ProductionRun>(e =>
            {
                e.ToTable("production_runs");
                e.HasKey(x => x.ProductionRunId);

                e.Property(x => x.RunCode)
                    .HasMaxLength(50);

                e.Property(x => x.Quantity)
                    .HasPrecision(18, 2);

                e.Property(x => x.Status)
                    .HasMaxLength(30);

                e.HasOne(x => x.ProductionPlan)
                    .WithMany(x => x.ProductionRuns)
                    .HasForeignKey(x => x.ProductionPlanId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.CentralKitchen)
                    .WithMany(x => x.ProductionRuns)
                    .HasForeignKey(x => x.CentralKitchenId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            // recipe / bom
            modelBuilder.Entity<Recipe>(e => { e.ToTable("recipes"); e.HasKey(x => x.RecipeId); });
            modelBuilder.Entity<Bom>(e => { e.ToTable("boms"); e.HasKey(x => x.BomId); });
            modelBuilder.Entity<BomItem>(e => { e.ToTable("bom_items"); e.HasKey(x => x.BomItemId); });

            // inventory
            modelBuilder.Entity<IngredientBatch>(e =>
            {
                e.ToTable("ingredient_batches", tb =>
                {
                    tb.HasCheckConstraint(
                        "CK_ingredient_batches_type_owner",
                        @"
                        (
                            ""Type"" = 'FRANCHISE'
                            AND ""FranchiseId"" IS NOT NULL
                            AND ""CentralKitchenId"" IS NULL
                        )
                        OR
                        (
                            ""Type"" = 'CENTRAL_KITCHEN'
                            AND ""FranchiseId"" IS NULL
                            AND ""CentralKitchenId"" IS NOT NULL
                        )");
                });

                e.HasKey(x => x.BatchId);

                e.Property(x => x.Type)
                    .IsRequired()
                    .HasMaxLength(50);

                e.HasOne(x => x.Franchise)
                    .WithMany(x => x.IngredientBatches)
                    .HasForeignKey(x => x.FranchiseId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.CentralKitchen)
                    .WithMany(x => x.IngredientBatches)
                    .HasForeignKey(x => x.CentralKitchenId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<InventoryMovement>(e => { e.ToTable("inventory_movements"); e.HasKey(x => x.MovementId); });

            modelBuilder.Entity<ProductBatch>(e =>
            {
                e.ToTable("product_batches", tb =>
                {
                    tb.HasCheckConstraint(
                        "CK_product_batches_owner",
                        @"
            (
                ""FranchiseId"" IS NOT NULL
                AND ""CentralKitchenId"" IS NULL
            )
            OR
            (
                ""FranchiseId"" IS NULL
                AND ""CentralKitchenId"" IS NOT NULL
            )");
                });

                e.HasKey(x => x.BatchId);

                e.Property(x => x.BatchCode)
                    .IsRequired()
                    .HasMaxLength(100);

                e.Property(x => x.Quantity)
                    .HasColumnType("numeric(18,2)");

                e.HasOne(x => x.Product)
                    .WithMany(x => x.ProductBatches)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Franchise)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.CentralKitchen)
                    .WithMany(x => x.ProductBatches)
                    .HasForeignKey(x => x.CentralKitchenId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ProductionRun)
                    .WithMany(x => x.ProductBatches)
                    .HasForeignKey(x => x.ProductionRunId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Franchise-owned uniqueness
                e.HasIndex(x => new { x.ProductId, x.FranchiseId, x.BatchCode })
                    .IsUnique()
                    .HasDatabaseName("UX_product_batches_product_franchise_batchcode")
                    .HasFilter(@"""FranchiseId"" IS NOT NULL AND ""CentralKitchenId"" IS NULL");

                // CentralKitchen-owned uniqueness
                e.HasIndex(x => new { x.ProductId, x.CentralKitchenId, x.BatchCode })
                    .IsUnique()
                    .HasDatabaseName("UX_product_batches_product_ck_batchcode")
                    .HasFilter(@"""CentralKitchenId"" IS NOT NULL AND ""FranchiseId"" IS NULL");
            });


            modelBuilder.Entity<ProductMovement>(e => { e.ToTable("product_movements"); e.HasKey(x => x.MovementId); });

            // delivery
            modelBuilder.Entity<DeliveryPlan>(e =>
            {
                e.ToTable("delivery_plans");
                e.HasKey(x => x.DeliveryPlanId);

                e.HasOne(x => x.Franchise)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.CentralKitchen)
                    .WithMany(x => x.DeliveryPlans)
                    .HasForeignKey(x => x.CentralKitchenId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Delivery>(e =>
            {
                e.ToTable("deliveries");
                e.HasKey(x => x.DeliveryId);

                e.HasOne(x => x.DeliveryPlan)
                    .WithMany(x => x.Deliveries)
                    .HasForeignKey(x => x.DeliveryPlanId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.FromCentralKitchen)
                    .WithMany()
                    .HasForeignKey(x => x.FromCentralKitchenId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<DeliveryProductItem>(e => { e.ToTable("delivery_product_items"); e.HasKey(x => x.DeliveryProductItemId); });
            modelBuilder.Entity<DeliveryIngredientItem>(e => { e.ToTable("delivery_ingredient_items"); e.HasKey(x => x.DeliveryIngredientItemId); });

            modelBuilder.Entity<ReceivingReport>(e => { e.ToTable("receiving_reports"); e.HasKey(x => x.ReceivingReportId); });

            // sales / support
            modelBuilder.Entity<SalesRecord>(e => { e.ToTable("sales_records"); e.HasKey(x => x.SalesRecordId); });
            modelBuilder.Entity<SupportRequest>(e => { e.ToTable("support_requests"); e.HasKey(x => x.SupportRequestId); });

            // audit / settings
            modelBuilder.Entity<AuditLog>(e => { e.ToTable("audit_logs"); e.HasKey(x => x.AuditLogId); });

            modelBuilder.Entity<SystemSetting>(e =>
            {
                e.ToTable("system_settings");
                e.HasKey(x => x.SystemSettingId);
            });

            // revoked tokens
            modelBuilder.Entity<RevokedToken>(e =>
            {
                e.ToTable("revoked_tokens");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedOnAdd();
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}