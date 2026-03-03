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

        private void ApplyTimestamps()
        {
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
                    continue;

                var createdAtProp = entry.Metadata.FindProperty("CreatedAt");
                var updatedAtProp = entry.Metadata.FindProperty("UpdatedAt");

                if (createdAtProp is null && updatedAtProp is null)
                    continue;

                if (entry.State == EntityState.Added)
                {
                    if (createdAtProp is not null)
                        TrySetDateTime(entry, "CreatedAt", now);

                    if (updatedAtProp is not null)
                        TrySetDateTime(entry, "UpdatedAt", now);
                }
                else
                {
                    if (createdAtProp is not null)
                        entry.Property("CreatedAt").IsModified = false;

                    if (updatedAtProp is not null)
                        TrySetDateTime(entry, "UpdatedAt", now);
                }
            }
        }

        private static void TrySetDateTime(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string propertyName, DateTime value)
        {
            var prop = entry.Metadata.FindProperty(propertyName);
            if (prop is null) return;

            if (prop.ClrType != typeof(DateTime) && prop.ClrType != typeof(DateTime?))
                return;

            entry.Property(propertyName).CurrentValue = value;
        }

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
            modelBuilder.Entity<Role>(e =>
            {
                e.ToTable("roles");
                e.HasKey(x => x.RoleId);
                e.HasIndex(x => x.Name).IsUnique();

                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("ACTIVE").IsRequired();
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
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
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
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

                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

                e.HasOne(x => x.Role)
                    .WithMany(x => x.Users)
                    .HasForeignKey(x => x.RoleId);
            });

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
                e.HasIndex(x => x.UserId).IsUnique();

                e.Property(x => x.AssignedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<Ingredient>(e =>
            {
                e.ToTable("ingredients");
                e.HasKey(x => x.IngredientId);

                e.Property(x => x.Status).HasDefaultValue("ACTIVE");
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
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

            modelBuilder.Entity<StoreCatalog>(e =>
            {
                e.ToTable("store_catalogs");
                e.HasKey(x => new { x.FranchiseId, x.ProductId });

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

            modelBuilder.Entity<Recipe>(e =>
            {
                e.ToTable("recipes");
                e.HasKey(x => x.RecipeId);

                e.HasIndex(x => new { x.ProductId, x.Version }).IsUnique();

                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("DRAFT").IsRequired();
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
                e.Property(x => x.Instructions).HasColumnType("text");
            });

            modelBuilder.Entity<Bom>(e =>
            {
                e.ToTable("boms");
                e.HasKey(x => x.BomId);

                e.HasIndex(x => new { x.ProductId, x.Version }).IsUnique();

                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("DRAFT").IsRequired();
                e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<BomItem>(e =>
            {
                e.ToTable("bom_items");
                e.HasKey(x => x.BomItemId);
            });

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

            modelBuilder.Entity<ProductBatch>(e =>
            {
                e.ToTable("product_batches");
                e.HasKey(x => x.BatchId);
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

            base.OnModelCreating(modelBuilder);
        }
    }
}