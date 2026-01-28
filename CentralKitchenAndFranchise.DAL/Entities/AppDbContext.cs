using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CentralKitchenAndFranchise.DAL.Entities
{
    using Microsoft.EntityFrameworkCore;

    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // =======================
        // ORGANIZATION & IDENTITY
        // =======================
        public DbSet<User> Users => Set<User>();
        public DbSet<Franchise> Franchises => Set<Franchise>();
        public DbSet<UserFranchise> UserFranchises => Set<UserFranchise>();

        // =======================
        // MASTER DATA
        // =======================
        public DbSet<Ingredient> Ingredients => Set<Ingredient>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<StoreCatalog> StoreCatalogs => Set<StoreCatalog>();

        // =======================
        // BOM & RECIPE
        // =======================
        public DbSet<Bom> Boms => Set<Bom>();
        public DbSet<BomItem> BomItems => Set<BomItem>();
        public DbSet<Recipe> Recipes => Set<Recipe>();

        // =======================
        // STORE ORDERING
        // =======================
        public DbSet<StoreOrder> StoreOrders => Set<StoreOrder>();
        public DbSet<StoreOrderItem> StoreOrderItems => Set<StoreOrderItem>();

        // =======================
        // DEMAND & ALLOCATION
        // =======================
        public DbSet<DemandAggregation> DemandAggregations => Set<DemandAggregation>();
        public DbSet<DemandItem> DemandItems => Set<DemandItem>();
        public DbSet<Allocation> Allocations => Set<Allocation>();
        public DbSet<AllocationItem> AllocationItems => Set<AllocationItem>();

        // =======================
        // INVENTORY & BATCH
        // =======================
        public DbSet<IngredientBatch> IngredientBatches => Set<IngredientBatch>();
        public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

        // =======================
        // PRODUCTION
        // =======================
        public DbSet<ProductionPlan> ProductionPlans => Set<ProductionPlan>();
        public DbSet<ProductionPlanItem> ProductionPlanItems => Set<ProductionPlanItem>();

        // =======================
        // DELIVERY
        // =======================
        public DbSet<DeliveryPlan> DeliveryPlans => Set<DeliveryPlan>();
        public DbSet<Delivery> Deliveries => Set<Delivery>();
        public DbSet<DeliveryItem> DeliveryItems => Set<DeliveryItem>();

        // =======================
        // RECEIVING & SALES
        // =======================
        public DbSet<ReceivingReport> ReceivingReports => Set<ReceivingReport>();
        public DbSet<SalesRecord> SalesRecords => Set<SalesRecord>();

        // =======================
        // SUPPORT & AUDIT
        // =======================
        public DbSet<SupportRequest> SupportRequests => Set<SupportRequest>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // USERS
            modelBuilder.Entity<User>(e =>
            {
                e.ToTable("users");
                e.HasKey(x => x.UserId);
                e.Property(x => x.UserId).HasColumnName("user_id");
                e.Property(x => x.Username).HasColumnName("username");
                e.Property(x => x.Email).HasColumnName("email");
                e.Property(x => x.PasswordHash).HasColumnName("password_hash");
                e.Property(x => x.Status).HasColumnName("status");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
                e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            });

            // FRANCHISE
            modelBuilder.Entity<Franchise>(e =>
            {
                e.ToTable("franchises");
                e.HasKey(x => x.FranchiseId);
                e.Property(x => x.FranchiseId).HasColumnName("franchise_id");
                e.Property(x => x.Name).HasColumnName("name");
                e.Property(x => x.Type).HasColumnName("type");
                e.Property(x => x.Address).HasColumnName("address");
                e.Property(x => x.Status).HasColumnName("status");
            });

            // USER_FRANCHISE (COMPOSITE)
            modelBuilder.Entity<UserFranchise>(e =>
            {
                e.ToTable("user_franchises");

                e.HasKey(x => new { x.UserId, x.FranchiseId });

                e.Property(x => x.UserId).HasColumnName("user_id");
                e.Property(x => x.FranchiseId).HasColumnName("franchise_id");
                e.Property(x => x.Role).HasColumnName("role");
                e.Property(x => x.Status).HasColumnName("status");
            });


            // INGREDIENTS
            modelBuilder.Entity<Ingredient>(e =>
            {
                e.ToTable("ingredients");
                e.HasKey(x => x.IngredientId);
                e.Property(x => x.IngredientId).HasColumnName("ingredient_id");
                e.Property(x => x.Name).HasColumnName("name");
                e.Property(x => x.Unit).HasColumnName("unit");
                e.Property(x => x.Status).HasColumnName("status");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
            });

            // PRODUCTS
            modelBuilder.Entity<Product>(e =>
            {
                e.ToTable("products");
                e.HasKey(x => x.ProductId);
                e.Property(x => x.ProductId).HasColumnName("product_id");
                e.Property(x => x.Sku).HasColumnName("sku");
                e.Property(x => x.Name).HasColumnName("name");
                e.Property(x => x.Unit).HasColumnName("unit");
                e.Property(x => x.Status).HasColumnName("status");
            });

            // STORE_CATALOG (COMPOSITE)
            modelBuilder.Entity<StoreCatalog>(e =>
            {
                e.ToTable("store_catalog");
                e.HasKey(x => new { x.FranchiseId, x.ProductId });
                e.Property(x => x.FranchiseId).HasColumnName("franchise_id");
                e.Property(x => x.ProductId).HasColumnName("product_id");
            });

            // BOM
            modelBuilder.Entity<Bom>(e =>
            {
                e.ToTable("boms");
                e.HasKey(x => x.BomId);
                e.Property(x => x.BomId).HasColumnName("bom_id");
                e.Property(x => x.ProductId).HasColumnName("product_id");
                e.Property(x => x.Version).HasColumnName("version");
                e.Property(x => x.Status).HasColumnName("status");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
            });

            // BOM_ITEMS (COMPOSITE)
            modelBuilder.Entity<BomItem>(e =>
            {
                e.ToTable("bom_items");
                e.HasKey(x => new { x.BomId, x.IngredientId });
                e.Property(x => x.BomId).HasColumnName("bom_id");
                e.Property(x => x.IngredientId).HasColumnName("ingredient_id");
                e.Property(x => x.Quantity).HasColumnName("quantity");
            });

            // STORE_ORDERS
            modelBuilder.Entity<StoreOrder>(e =>
            {
                e.ToTable("store_orders");
                e.HasKey(x => x.StoreOrderId);
                e.Property(x => x.StoreOrderId).HasColumnName("store_order_id");
                e.Property(x => x.FranchiseId).HasColumnName("franchise_id");
                e.Property(x => x.OrderDate).HasColumnName("order_date").HasColumnType("date");
                e.Property(x => x.Status).HasColumnName("status");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
            });

            modelBuilder.Entity<StoreOrderItem>(e =>
            {
                e.ToTable("store_order_items");
                e.HasKey(x => new { x.StoreOrderId, x.ProductId });
                e.Property(x => x.StoreOrderId).HasColumnName("store_order_id");
                e.Property(x => x.ProductId).HasColumnName("product_id");
                e.Property(x => x.Quantity).HasColumnName("quantity");
            });

            // DEMAND
            modelBuilder.Entity<DemandAggregation>(e =>
            {
                e.ToTable("demand_aggregations");
                e.HasKey(x => x.DemandAggregationId);
                e.Property(x => x.DemandAggregationId).HasColumnName("demand_aggregation_id");
                e.Property(x => x.RunAt).HasColumnName("run_at");
            });

            modelBuilder.Entity<DemandItem>(e =>
            {
                e.ToTable("demand_items");
                e.HasKey(x => new { x.DemandAggregationId, x.ProductId });
                e.Property(x => x.DemandAggregationId).HasColumnName("demand_aggregation_id");
                e.Property(x => x.ProductId).HasColumnName("product_id");
                e.Property(x => x.TotalQuantity).HasColumnName("total_quantity");
            });
            modelBuilder.Entity<Supplier>(e =>
            {
                e.ToTable("suppliers");
                e.HasKey(x => x.SupplierId);
                e.Property(x => x.SupplierId).HasColumnName("supplier_id");
                e.Property(x => x.Name).HasColumnName("name");
                e.Property(x => x.ContactInfo).HasColumnName("contact_info");
                e.Property(x => x.Status).HasColumnName("status");
            });
            modelBuilder.Entity<Recipe>(e =>
            {
                e.ToTable("recipes");
                e.HasKey(x => x.RecipeId);
                e.Property(x => x.RecipeId).HasColumnName("recipe_id");
                e.Property(x => x.ProductId).HasColumnName("product_id");
                e.Property(x => x.Instructions).HasColumnName("instructions");
                e.Property(x => x.Status).HasColumnName("status");
            });


            // ALLOCATION
            modelBuilder.Entity<Allocation>(e =>
            {
                e.ToTable("allocations");
                e.HasKey(x => x.AllocationId);
                e.Property(x => x.AllocationId).HasColumnName("allocation_id");
                e.Property(x => x.DemandAggregationId).HasColumnName("demand_aggregation_id");
                e.Property(x => x.ApprovedBy).HasColumnName("approved_by");
                e.Property(x => x.ApprovedAt).HasColumnName("approved_at");
            });

            modelBuilder.Entity<AllocationItem>(e =>
            {
                e.ToTable("allocation_items");
                e.HasKey(x => new { x.AllocationId, x.FranchiseId, x.ProductId });
                e.Property(x => x.AllocationId).HasColumnName("allocation_id");
                e.Property(x => x.FranchiseId).HasColumnName("franchise_id");
                e.Property(x => x.ProductId).HasColumnName("product_id");
                e.Property(x => x.AllocatedQuantity).HasColumnName("allocated_quantity");
            });

            // INVENTORY
            modelBuilder.Entity<IngredientBatch>(e =>
            {
                e.ToTable("ingredient_batches");
                e.HasKey(x => x.IngredientBatchId);
                e.Property(x => x.IngredientBatchId).HasColumnName("ingredient_batch_id");
                e.Property(x => x.IngredientId).HasColumnName("ingredient_id");
                e.Property(x => x.BatchCode).HasColumnName("batch_code");
                e.Property(x => x.ExpiryDate).HasColumnName("expiry_date").HasColumnType("date");
                e.Property(x => x.Quantity).HasColumnName("quantity");
                e.Property(x => x.FranchiseId).HasColumnName("franchise_id");
            });

            modelBuilder.Entity<InventoryMovement>(e =>
            {
                e.ToTable("inventory_movements");
                e.HasKey(x => x.InventoryMovementId);
                e.Property(x => x.InventoryMovementId).HasColumnName("inventory_movement_id");
                e.Property(x => x.IngredientBatchId).HasColumnName("ingredient_batch_id");
                e.Property(x => x.Type).HasColumnName("type");
                e.Property(x => x.Quantity).HasColumnName("quantity");
                e.Property(x => x.Reason).HasColumnName("reason");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
            });

            // PRODUCTION
            modelBuilder.Entity<ProductionPlan>(e =>
            {
                e.ToTable("production_plans");
                e.HasKey(x => x.ProductionPlanId);
                e.Property(x => x.ProductionPlanId).HasColumnName("production_plan_id");
                e.Property(x => x.KitchenFranchiseId).HasColumnName("kitchen_franchise_id");
                e.Property(x => x.Status).HasColumnName("status");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
            });

            modelBuilder.Entity<ProductionPlanItem>(e =>
            {
                e.ToTable("production_plan_items");
                e.HasKey(x => new { x.ProductionPlanId, x.ProductId });
                e.Property(x => x.ProductionPlanId).HasColumnName("production_plan_id");
                e.Property(x => x.ProductId).HasColumnName("product_id");
                e.Property(x => x.Quantity).HasColumnName("quantity");
            });

            // DELIVERY
            modelBuilder.Entity<DeliveryPlan>(e =>
            {
                e.ToTable("delivery_plans");
                e.HasKey(x => x.DeliveryPlanId);
                e.Property(x => x.DeliveryPlanId).HasColumnName("delivery_plan_id");
                e.Property(x => x.PlannedDate).HasColumnName("planned_date").HasColumnType("date");
                e.Property(x => x.Status).HasColumnName("status");
            });

            modelBuilder.Entity<Delivery>(e =>
            {
                e.ToTable("deliveries");
                e.HasKey(x => x.DeliveryId);
                e.Property(x => x.DeliveryId).HasColumnName("delivery_id");
                e.Property(x => x.DeliveryPlanId).HasColumnName("delivery_plan_id");
                e.Property(x => x.FranchiseId).HasColumnName("franchise_id");
                e.Property(x => x.Status).HasColumnName("status");
            });

            modelBuilder.Entity<DeliveryItem>(e =>
            {
                e.ToTable("delivery_items");
                e.HasKey(x => new { x.DeliveryId, x.ProductId });
                e.Property(x => x.DeliveryId).HasColumnName("delivery_id");
                e.Property(x => x.ProductId).HasColumnName("product_id");
                e.Property(x => x.Quantity).HasColumnName("quantity");
            });

            // RECEIVING & SALES
            modelBuilder.Entity<ReceivingReport>(e =>
            {
                e.ToTable("receiving_reports");
                e.HasKey(x => x.ReceivingReportId);
                e.Property(x => x.ReceivingReportId).HasColumnName("receiving_report_id");
                e.Property(x => x.DeliveryId).HasColumnName("delivery_id");
                e.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
            });

            modelBuilder.Entity<SalesRecord>(e =>
            {
                e.ToTable("sales_records");
                e.HasKey(x => x.SalesRecordId);
                e.Property(x => x.SalesRecordId).HasColumnName("sales_record_id");
                e.Property(x => x.FranchiseId).HasColumnName("franchise_id");
                e.Property(x => x.ProductId).HasColumnName("product_id");
                e.Property(x => x.Quantity).HasColumnName("quantity");
                e.Property(x => x.SoldAt).HasColumnName("sold_at");
            });

            // SUPPORT & AUDIT
            modelBuilder.Entity<SupportRequest>(e =>
            {
                e.ToTable("support_requests");
                e.HasKey(x => x.SupportRequestId);
                e.Property(x => x.SupportRequestId).HasColumnName("support_request_id");
                e.Property(x => x.UserId).HasColumnName("user_id");
                e.Property(x => x.Message).HasColumnName("message");
                e.Property(x => x.Status).HasColumnName("status");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
            });

            modelBuilder.Entity<AuditLog>(e =>
            {
                e.ToTable("audit_logs");
                e.HasKey(x => x.AuditLogId);
                e.Property(x => x.AuditLogId).HasColumnName("audit_log_id");
                e.Property(x => x.ActorId).HasColumnName("actor_id");
                e.Property(x => x.Action).HasColumnName("action");
                e.Property(x => x.Entity).HasColumnName("entity");
                e.Property(x => x.CreatedAt).HasColumnName("created_at");
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}