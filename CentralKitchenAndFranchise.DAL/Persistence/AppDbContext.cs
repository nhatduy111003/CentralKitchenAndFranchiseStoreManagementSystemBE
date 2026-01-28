using CentralKitchenAndFranchise.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.DAL.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Franchise> Franchises => Set<Franchise>();
    public DbSet<UserFranchise> UserFranchises => Set<UserFranchise>();

    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();

    public DbSet<StoreCatalog> StoreCatalog => Set<StoreCatalog>();
    public DbSet<StoreOrder> StoreOrders => Set<StoreOrder>();
    public DbSet<StoreOrderItem> StoreOrderItems => Set<StoreOrderItem>();

    public DbSet<Bom> Boms => Set<Bom>();
    public DbSet<BomItem> BomItems => Set<BomItem>();

    public DbSet<IngredientBatch> IngredientBatches => Set<IngredientBatch>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<ProductionPlan> ProductionPlans => Set<ProductionPlan>();
    public DbSet<ProductionPlanItem> ProductionPlanItems => Set<ProductionPlanItem>();

    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<SalesRecord> SalesRecords => Set<SalesRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // roles
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        // users
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.RoleId).HasColumnName("role_id").IsRequired();

            e.Property(x => x.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired().HasDefaultValue("ACTIVE");

            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("now()");

            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();

            e.HasOne(x => x.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // franchises
        modelBuilder.Entity<Franchise>(e =>
        {
            e.ToTable("franchises");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            e.Property(x => x.Type).HasColumnName("type").HasMaxLength(30).IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
            e.Property(x => x.Address).HasColumnName("address");
            e.Property(x => x.Location).HasColumnName("location").HasMaxLength(255);
        });

        // user_franchises (composite key)
        modelBuilder.Entity<UserFranchise>(e =>
        {
            e.ToTable("user_franchises");
            e.HasKey(x => new { x.UserId, x.FranchiseId });

            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.FranchiseId).HasColumnName("franchise_id");
            e.Property(x => x.AssignedAt).HasColumnName("assigned_at").IsRequired().HasDefaultValueSql("now()");

            e.HasOne(x => x.User)
                .WithMany(u => u.UserFranchises)
                .HasForeignKey(x => x.UserId);

            e.HasOne(x => x.Franchise)
                .WithMany(f => f.UserFranchises)
                .HasForeignKey(x => x.FranchiseId);
        });

        // ingredients
        modelBuilder.Entity<Ingredient>(e =>
        {
            e.ToTable("ingredients");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            e.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(50).IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired().HasDefaultValue("ACTIVE");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

            e.HasIndex(x => x.Name).IsUnique();
        });

        // suppliers
        modelBuilder.Entity<Supplier>(e =>
        {
            e.ToTable("suppliers");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            e.Property(x => x.ContactInfo).HasColumnName("contact_info");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired().HasDefaultValue("ACTIVE");

            e.HasIndex(x => x.Name).IsUnique();
        });

        // products
        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            e.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(100).IsRequired();
            e.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(50).IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired().HasDefaultValue("ACTIVE");

            e.HasIndex(x => x.Sku).IsUnique();
        });

        // store_catalog (composite key)
        modelBuilder.Entity<StoreCatalog>(e =>
        {
            e.ToTable("store_catalog");
            e.HasKey(x => new { x.StoreFranchiseId, x.ProductId });

            e.Property(x => x.StoreFranchiseId).HasColumnName("store_franchise_id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.Price).HasColumnName("price").HasPrecision(12, 2).IsRequired();

            e.HasOne(x => x.StoreFranchise)
                .WithMany(f => f.StoreCatalogs)
                .HasForeignKey(x => x.StoreFranchiseId);

            e.HasOne(x => x.Product)
                .WithMany(p => p.StoreCatalogs)
                .HasForeignKey(x => x.ProductId);
        });

        // store_orders
        modelBuilder.Entity<StoreOrder>(e =>
        {
            e.ToTable("store_orders");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.StoreFranchiseId).HasColumnName("store_franchise_id").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

            e.HasOne(x => x.StoreFranchise)
                .WithMany(f => f.StoreOrders)
                .HasForeignKey(x => x.StoreFranchiseId);
        });

        // store_order_items
        modelBuilder.Entity<StoreOrderItem>(e =>
        {
            e.ToTable("store_order_items");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.OrderId).HasColumnName("order_id").IsRequired();
            e.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 3).IsRequired();

            e.HasOne(x => x.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(x => x.OrderId);

            e.HasOne(x => x.Product)
                .WithMany(p => p.StoreOrderItems)
                .HasForeignKey(x => x.ProductId);
        });

        // boms
        modelBuilder.Entity<Bom>(e =>
        {
            e.ToTable("boms");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            e.Property(x => x.Version).HasColumnName("version").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

            e.HasIndex(x => new { x.ProductId, x.Version }).IsUnique();

            e.HasOne(x => x.Product)
                .WithMany(p => p.Boms)
                .HasForeignKey(x => x.ProductId);
        });

        // bom_items
        modelBuilder.Entity<BomItem>(e =>
        {
            e.ToTable("bom_items");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.BomId).HasColumnName("bom_id").IsRequired();
            e.Property(x => x.IngredientId).HasColumnName("ingredient_id").IsRequired();
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 3).IsRequired();

            e.HasOne(x => x.Bom)
                .WithMany(b => b.Items)
                .HasForeignKey(x => x.BomId);

            e.HasOne(x => x.Ingredient)
                .WithMany(i => i.BomItems)
                .HasForeignKey(x => x.IngredientId);
        });

        // ingredient_batches
        modelBuilder.Entity<IngredientBatch>(e =>
        {
            e.ToTable("ingredient_batches");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.IngredientId).HasColumnName("ingredient_id").IsRequired();
            e.Property(x => x.FranchiseId).HasColumnName("franchise_id").IsRequired();
            e.Property(x => x.BatchCode).HasColumnName("batch_code").HasMaxLength(100).IsRequired();
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 3).IsRequired();
            e.Property(x => x.ExpiredAt).HasColumnName("expired_at");

            e.HasIndex(x => new { x.IngredientId, x.BatchCode, x.FranchiseId }).IsUnique();

            e.HasOne(x => x.Ingredient)
                .WithMany(i => i.IngredientBatches)
                .HasForeignKey(x => x.IngredientId);

            e.HasOne(x => x.Franchise)
                .WithMany(f => f.IngredientBatches)
                .HasForeignKey(x => x.FranchiseId);
        });

        // inventory_movements
        modelBuilder.Entity<InventoryMovement>(e =>
        {
            e.ToTable("inventory_movements");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.BatchId).HasColumnName("batch_id").IsRequired();
            e.Property(x => x.Type).HasColumnName("type").HasMaxLength(20).IsRequired();
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 3).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

            e.HasOne(x => x.Batch)
                .WithMany(b => b.InventoryMovements)
                .HasForeignKey(x => x.BatchId);
        });

        // production_plans
        modelBuilder.Entity<ProductionPlan>(e =>
        {
            e.ToTable("production_plans");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.PlanDate).HasColumnName("plan_date").IsRequired();
            e.Property(x => x.FranchiseId).HasColumnName("franchise_id").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

            e.HasOne(x => x.Franchise)
                .WithMany(f => f.ProductionPlans)
                .HasForeignKey(x => x.FranchiseId);
        });

        // production_plan_items
        modelBuilder.Entity<ProductionPlanItem>(e =>
        {
            e.ToTable("production_plan_items");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.PlanId).HasColumnName("plan_id").IsRequired();
            e.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 3).IsRequired();

            e.HasOne(x => x.Plan)
                .WithMany(p => p.Items)
                .HasForeignKey(x => x.PlanId);

            e.HasOne(x => x.Product)
                .WithMany(p => p.ProductionPlanItems)
                .HasForeignKey(x => x.ProductId);
        });

        // deliveries
        modelBuilder.Entity<Delivery>(e =>
        {
            e.ToTable("deliveries");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.StoreFranchiseId).HasColumnName("store_franchise_id").IsRequired();
            e.Property(x => x.DeliveredAt).HasColumnName("delivered_at").IsRequired().HasDefaultValueSql("now()");

            e.HasOne(x => x.StoreFranchise)
                .WithMany(f => f.Deliveries)
                .HasForeignKey(x => x.StoreFranchiseId);
        });

        // sales_records
        modelBuilder.Entity<SalesRecord>(e =>
        {
            e.ToTable("sales_records");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.StoreFranchiseId).HasColumnName("store_franchise_id").IsRequired();
            e.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            e.Property(x => x.SoldAt).HasColumnName("sold_at").IsRequired();
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 3).IsRequired();

            e.HasOne(x => x.StoreFranchise)
                .WithMany(f => f.SalesRecords)
                .HasForeignKey(x => x.StoreFranchiseId);

            e.HasOne(x => x.Product)
                .WithMany(p => p.SalesRecords)
                .HasForeignKey(x => x.ProductId);
        });

        // ---- minimal seed (optional)
        // hardcode timestamp to keep migrations stable
        var seedTs = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Admin" },
            new Role { Id = 2, Name = "Manager" },
            new Role { Id = 3, Name = "SupplyCoordinator" },
            new Role { Id = 4, Name = "KitchenStaff" },
            new Role { Id = 5, Name = "StoreStaff" }
        );

        modelBuilder.Entity<User>().HasData(new User
        {
            Id = 1,
            RoleId = 1,
            Username = "admin",
            Email = "admin@gmail.com",

            // password = 123456
            PasswordHash = "$2a$11$LX2vA6.3yfqwNgiFTE4Gbu.WN8hT/YwH8mBIqwA4SjtWaDiD0D7PC",

            Status = "ACTIVE",
            CreatedAt = seedTs,
            UpdatedAt = seedTs
        });

    }
}
