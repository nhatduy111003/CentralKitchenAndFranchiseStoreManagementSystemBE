using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.Enums;
using CentralKitchenAndFranchise.DTO.Constants;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace CentralKitchenAndFranchise.DAL.Seeding;

public static class DbSeeder
{
    private const string DefaultPassword = "123456";

    private const string CentralKitchenName = "Central Kitchen - HCMC";
    private const string FranchiseQ1Name = "Franchise Store - District 1";
    private const string FranchiseQ7Name = "Franchise Store - District 7";

    public static void Seed(AppDbContext db)
    {
        var now = DateTime.UtcNow;

        SeedRoles(db);
        SeedOrganizations(db, now);
        SeedUsersAndAssignments(db, now);
        SeedSystemSettings(db, now);
        SeedSuppliers(db);
        SeedMasterData(db, now);
        SeedRecipesAndBoms(db, now);
        SeedCentralKitchenIngredientInventory(db, now);
        SeedStoreOrders(db, now);
        SeedDemandAggregations(db, now);
        SeedProductionPlansAndRuns(db, now);
        SeedCentralKitchenProductInventory(db, now);

        db.SaveChanges();
    }

    // ==================================================
    // 1) Roles
    // ==================================================
    private static void SeedRoles(AppDbContext db)
    {
        var requiredRoles = new[]
        {
            RoleNames.Admin,
            RoleNames.Manager,
            RoleNames.SupplyCoordinator,
            RoleNames.KitchenStaff,
            RoleNames.StoreStaff
        };

        var existing = db.Roles
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in requiredRoles)
        {
            if (existing.Contains(roleName)) continue;

            db.Roles.Add(new Role
            {
                Name = roleName
            });
        }

        db.SaveChanges();
    }

    // ==================================================
    // 2) CentralKitchen + Franchise
    // ==================================================
    private static void SeedOrganizations(AppDbContext db, DateTime now)
    {
        var centralKitchen = EnsureCentralKitchen(
            db,
            name: CentralKitchenName,
            address: "Warehouse 01, Tan Binh, Ho Chi Minh City",
            location: "Ho Chi Minh City",
            latitude: 10.8019,
            longitude: 106.6522,
            now: now);

        db.SaveChanges();

        EnsureFranchise(
            db,
            centralKitchenId: centralKitchen.CentralKitchenId,
            name: FranchiseQ1Name,
            type: "STORE",
            address: "Nguyen Hue, District 1, Ho Chi Minh City",
            location: "Ho Chi Minh City",
            latitude: 10.7756,
            longitude: 106.7033,
            now: now);

        EnsureFranchise(
            db,
            centralKitchenId: centralKitchen.CentralKitchenId,
            name: FranchiseQ7Name,
            type: "STORE",
            address: "Phu My Hung, District 7, Ho Chi Minh City",
            location: "Ho Chi Minh City",
            latitude: 10.7290,
            longitude: 106.7218,
            now: now);

        db.SaveChanges();
    }

    private static CentralKitchen EnsureCentralKitchen(
        AppDbContext db,
        string name,
        string? address,
        string? location,
        double? latitude,
        double? longitude,
        DateTime now)
    {
        var existing = db.CentralKitchens.FirstOrDefault(x => x.Name.ToLower() == name.ToLower());
        if (existing != null)
        {
            var changed = false;

            if (!string.Equals(existing.Status, OrganizationStatus.Active, StringComparison.OrdinalIgnoreCase))
            {
                existing.Status = OrganizationStatus.Active;
                changed = true;
            }

            if (existing.Address != address)
            {
                existing.Address = address;
                changed = true;
            }

            if (existing.Location != location)
            {
                existing.Location = location;
                changed = true;
            }

            if (existing.Latitude != latitude)
            {
                existing.Latitude = latitude;
                changed = true;
            }

            if (existing.Longitude != longitude)
            {
                existing.Longitude = longitude;
                changed = true;
            }

            if (changed)
            {
                existing.UpdatedAt = now;
            }

            return existing;
        }

        var created = new CentralKitchen
        {
            Name = name,
            Status = OrganizationStatus.Active,
            Address = address,
            Location = location,
            Latitude = latitude,
            Longitude = longitude,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.CentralKitchens.Add(created);
        return created;
    }

    private static Franchise EnsureFranchise(
        AppDbContext db,
        int centralKitchenId,
        string name,
        string type,
        string? address,
        string? location,
        double? latitude,
        double? longitude,
        DateTime now)
    {
        var existing = db.Franchises.FirstOrDefault(x => x.Name.ToLower() == name.ToLower());
        if (existing != null)
        {
            var changed = false;

            if (existing.CentralKitchenId != centralKitchenId)
            {
                existing.CentralKitchenId = centralKitchenId;
                changed = true;
            }

            if (!string.Equals(existing.Type, type, StringComparison.OrdinalIgnoreCase))
            {
                existing.Type = type;
                changed = true;
            }

            if (!string.Equals(existing.Status, OrganizationStatus.Active, StringComparison.OrdinalIgnoreCase))
            {
                existing.Status = OrganizationStatus.Active;
                changed = true;
            }

            if (existing.Address != address)
            {
                existing.Address = address;
                changed = true;
            }

            if (existing.Location != location)
            {
                existing.Location = location;
                changed = true;
            }

            if (existing.Latitude != latitude)
            {
                existing.Latitude = latitude;
                changed = true;
            }

            if (existing.Longitude != longitude)
            {
                existing.Longitude = longitude;
                changed = true;
            }

            if (changed)
            {
                existing.UpdatedAt = now;
            }

            return existing;
        }

        var created = new Franchise
        {
            CentralKitchenId = centralKitchenId,
            Name = name,
            Type = type,
            Status = OrganizationStatus.Active,
            Address = address,
            Location = location,
            Latitude = latitude,
            Longitude = longitude,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Franchises.Add(created);
        return created;
    }

    // ==================================================
    // 3) Users + UserWorkAssignment
    // ==================================================
    private static void SeedUsersAndAssignments(AppDbContext db, DateTime now)
    {
        var adminRoleId = GetRoleId(db, RoleNames.Admin);
        var managerRoleId = GetRoleId(db, RoleNames.Manager);
        var supplyRoleId = GetRoleId(db, RoleNames.SupplyCoordinator);
        var kitchenRoleId = GetRoleId(db, RoleNames.KitchenStaff);
        var storeRoleId = GetRoleId(db, RoleNames.StoreStaff);

        var admin = EnsureUser(db, "admin", "admin@gmail.com", adminRoleId, now);
        var manager = EnsureUser(db, "manager", "manager@gmail.com", managerRoleId, now);
        var supply = EnsureUser(db, "supply", "supply@gmail.com", supplyRoleId, now);
        var kitchen = EnsureUser(db, "kitchen", "kitchen@gmail.com", kitchenRoleId, now);
        var storeQ1 = EnsureUser(db, "store.q1", "store.q1@gmail.com", storeRoleId, now);
        var storeQ7 = EnsureUser(db, "store.q7", "store.q7@gmail.com", storeRoleId, now);

        var centralKitchen = db.CentralKitchens.First(x => x.Name == CentralKitchenName);
        var frQ1 = db.Franchises.First(x => x.Name == FranchiseQ1Name);
        var frQ7 = db.Franchises.First(x => x.Name == FranchiseQ7Name);

        // Global roles
        EnsureNoWorkAssignment(db, admin.UserId);
        EnsureNoWorkAssignment(db, manager.UserId);

        // CK-scoped roles
        EnsureUserWorkAssignment(
            db,
            supply.UserId,
            WorkAssignmentTypes.CentralKitchen,
            franchiseId: null,
            centralKitchenId: centralKitchen.CentralKitchenId,
            now: now);

        EnsureUserWorkAssignment(
            db,
            kitchen.UserId,
            WorkAssignmentTypes.CentralKitchen,
            franchiseId: null,
            centralKitchenId: centralKitchen.CentralKitchenId,
            now: now);

        // Franchise-scoped roles
        EnsureUserWorkAssignment(
            db,
            storeQ1.UserId,
            WorkAssignmentTypes.Franchise,
            franchiseId: frQ1.FranchiseId,
            centralKitchenId: null,
            now: now);

        EnsureUserWorkAssignment(
            db,
            storeQ7.UserId,
            WorkAssignmentTypes.Franchise,
            franchiseId: frQ7.FranchiseId,
            centralKitchenId: null,
            now: now);

        db.SaveChanges();
    }

    private static int GetRoleId(AppDbContext db, string roleName)
    {
        var roleId = db.Roles
            .Where(x => x.Name == roleName)
            .Select(x => x.RoleId)
            .FirstOrDefault();

        if (roleId == 0)
            throw new InvalidOperationException($"Role not found: {roleName}");

        return roleId;
    }

    private static User EnsureUser(
        AppDbContext db,
        string username,
        string email,
        int roleId,
        DateTime now)
    {
        var usernameKey = username.ToLower();
        var emailKey = email.ToLower();

        var existing = db.Users.FirstOrDefault(x =>
            x.Username.ToLower() == usernameKey ||
            x.Email.ToLower() == emailKey);

        if (existing != null)
        {
            var changed = false;

            if (existing.RoleId != roleId)
            {
                existing.RoleId = roleId;
                changed = true;
            }

            if (!string.Equals(existing.Status, OrganizationStatus.Active, StringComparison.OrdinalIgnoreCase))
            {
                existing.Status = OrganizationStatus.Active;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(existing.PasswordHash))
            {
                existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword);
                changed = true;
            }

            if (changed)
            {
                existing.UpdatedAt = now;
            }

            return existing;
        }

        var created = new User
        {
            Username = username,
            Email = email,
            RoleId = roleId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            Status = OrganizationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.Add(created);
        db.SaveChanges();
        return created;
    }

    private static void EnsureNoWorkAssignment(AppDbContext db, int userId)
    {
        var assignments = db.UserWorkAssignments.Where(x => x.UserId == userId).ToList();
        if (assignments.Count == 0) return;

        db.UserWorkAssignments.RemoveRange(assignments);
    }

    private static void EnsureUserWorkAssignment(
        AppDbContext db,
        int userId,
        string assignmentType,
        int? franchiseId,
        int? centralKitchenId,
        DateTime now)
    {
        var existing = db.UserWorkAssignments.FirstOrDefault(x => x.UserId == userId);

        if (existing == null)
        {
            db.UserWorkAssignments.Add(new UserWorkAssignment
            {
                UserId = userId,
                AssignmentType = assignmentType,
                FranchiseId = franchiseId,
                CentralKitchenId = centralKitchenId,
                AssignedAt = now
            });
            return;
        }

        var changed =
            !string.Equals(existing.AssignmentType, assignmentType, StringComparison.OrdinalIgnoreCase) ||
            existing.FranchiseId != franchiseId ||
            existing.CentralKitchenId != centralKitchenId;

        if (!changed) return;

        existing.AssignmentType = assignmentType;
        existing.FranchiseId = franchiseId;
        existing.CentralKitchenId = centralKitchenId;
        existing.AssignedAt = now;
    }

    // ==================================================
    // 4) System settings
    // ==================================================
    private static void SeedSystemSettings(AppDbContext db, DateTime now)
    {
        EnsureSystemSetting(
            db,
            SettingKeys.NearExpiryDays,
            "7",
            "Near-expiry definition window in days",
            now);

        EnsureSystemSetting(
            db,
            SettingKeys.FutureOrderLimitDays,
            "7",
            "Max future order creation window in days",
            now);

        EnsureSystemSetting(
            db,
            SettingKeys.OrderEditWindowMinutes,
            "30",
            "Allowed order edit window after submit in minutes",
            now);

        EnsureSystemSetting(
            db,
            SettingKeys.CutoffTime,
            "17:00",
            "Daily cutoff time for store orders (HH:mm)",
            now);

        db.SaveChanges();
    }

    private static void EnsureSystemSetting(
        AppDbContext db,
        string key,
        string value,
        string description,
        DateTime now)
    {
        var existing = db.SystemSettings.FirstOrDefault(x => x.Key == key);
        if (existing != null)
        {
            var changed = false;

            if (existing.Value != value)
            {
                existing.Value = value;
                changed = true;
            }

            if (existing.Description != description)
            {
                existing.Description = description;
                changed = true;
            }

            if (changed)
            {
                existing.UpdatedAt = now;
            }

            return;
        }

        db.SystemSettings.Add(new SystemSetting
        {
            Key = key,
            Value = value,
            Description = description,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    // ==================================================
    // 5) Suppliers
    // ==================================================
    private static void SeedSuppliers(AppDbContext db)
    {
        EnsureSupplier(
            db,
            "Viet Tea Supply Co.",
            "sales@viettea.local | +84 28 1111 1111");

        EnsureSupplier(
            db,
            "Saigon Dairy & Powder",
            "sales@saigondairy.local | +84 28 2222 2222");

        EnsureSupplier(
            db,
            "Packaging Hub",
            "support@packaginghub.local | +84 28 3333 3333");

        db.SaveChanges();
    }

    private static void EnsureSupplier(AppDbContext db, string name, string? contactInfo)
    {
        var existing = db.Suppliers.FirstOrDefault(x => x.Name.ToLower() == name.ToLower());
        if (existing != null)
        {
            existing.ContactInfo = contactInfo;
            existing.Status = SupplierStatus.Active;
            return;
        }

        db.Suppliers.Add(new Supplier
        {
            Name = name,
            ContactInfo = contactInfo,
            Status = SupplierStatus.Active
        });
    }

    // ==================================================
    // 6) Ingredients + Products + StoreCatalog
    // ==================================================
    private static void SeedMasterData(AppDbContext db, DateTime now)
    {
        var ingredients = new[]
        {
            new SeedIngredient("Black Tea Leaves", "g", 0.06m, 1000m, 100m, 365),
            new SeedIngredient("Oolong Tea Leaves", "g", 0.08m, 1000m, 100m, 365),
            new SeedIngredient("Jasmine Green Tea Leaves", "g", 0.07m, 1000m, 100m, 365),
            new SeedIngredient("Milk Powder", "g", 0.10m, 1500m, 100m, 270),
            new SeedIngredient("Non-dairy Creamer", "g", 0.09m, 1500m, 100m, 270),
            new SeedIngredient("Sugar Syrup", "ml", 0.02m, 3000m, 0m, 120),
            new SeedIngredient("Brown Sugar", "g", 0.03m, 2000m, 0m, 365),
            new SeedIngredient("Tapioca Pearls (Dry)", "g", 0.04m, 2000m, 0m, 240),
            new SeedIngredient("Taro Powder", "g", 0.12m, 1000m, 0m, 240),
            new SeedIngredient("Water", "ml", 0.0005m, 50000m, 0m, 365),
            new SeedIngredient("Ice Cubes", "g", 0.001m, 5000m, 0m, 7),
            new SeedIngredient("Cup 500ml", "pcs", 0.20m, 500m, 0m, 3650),
            new SeedIngredient("Lid 500ml", "pcs", 0.08m, 500m, 0m, 3650),
            new SeedIngredient("Straw", "pcs", 0.03m, 500m, 0m, 3650),
        };

        foreach (var item in ingredients)
        {
            EnsureIngredient(db, item, now);
        }

        db.SaveChanges();

        var products = new[]
        {
            new SeedProduct("Classic Milk Tea 500ml", "FT-CLMT-500", "cup", ProductTypes.Finished, 2),
            new SeedProduct("Brown Sugar Milk Tea 500ml", "FT-BSMT-500", "cup", ProductTypes.Finished, 2),
            new SeedProduct("Taro Milk Tea 500ml", "FT-TARO-500", "cup", ProductTypes.Finished, 2),

            new SeedProduct("Brown Sugar Syrup (Batch)", "SF-BSS-001", "ml", ProductTypes.SemiFinished, 2),
            new SeedProduct("Black Tea Concentrate (Batch)", "SF-BT-001", "ml", ProductTypes.SemiFinished, 2),
            new SeedProduct("Cooked Tapioca Pearls (Batch)", "SF-PEARL-001", "g", ProductTypes.SemiFinished, 2),
        };

        foreach (var product in products)
        {
            EnsureProduct(db, product);
        }

        db.SaveChanges();

        var frQ1 = db.Franchises.First(x => x.Name == FranchiseQ1Name);
        var frQ7 = db.Franchises.First(x => x.Name == FranchiseQ7Name);

        var finishedProducts = db.Products
            .Where(x => x.ProductType == ProductTypes.Finished)
            .ToDictionary(x => x.Sku, x => x);

        EnsureStoreCatalog(db, frQ1.FranchiseId, finishedProducts["FT-CLMT-500"].ProductId, 35000m, now);
        EnsureStoreCatalog(db, frQ1.FranchiseId, finishedProducts["FT-BSMT-500"].ProductId, 42000m, now);
        EnsureStoreCatalog(db, frQ1.FranchiseId, finishedProducts["FT-TARO-500"].ProductId, 40000m, now);

        EnsureStoreCatalog(db, frQ7.FranchiseId, finishedProducts["FT-CLMT-500"].ProductId, 34000m, now);
        EnsureStoreCatalog(db, frQ7.FranchiseId, finishedProducts["FT-BSMT-500"].ProductId, 41000m, now);
        EnsureStoreCatalog(db, frQ7.FranchiseId, finishedProducts["FT-TARO-500"].ProductId, 39000m, now);

        db.SaveChanges();
    }

    private static void EnsureIngredient(AppDbContext db, SeedIngredient seed, DateTime now)
    {
        var existing = db.Ingredients.FirstOrDefault(x => x.Name.ToLower() == seed.Name.ToLower());
        if (existing != null)
        {
            var changed = false;

            if (existing.Unit != seed.Unit)
            {
                existing.Unit = seed.Unit;
                changed = true;
            }

            if (!string.Equals(existing.Status, IngredientStatus.Active, StringComparison.OrdinalIgnoreCase))
            {
                existing.Status = IngredientStatus.Active;
                changed = true;
            }

            if (existing.Price != seed.Price)
            {
                existing.Price = seed.Price;
                changed = true;
            }

            if (existing.SafetyStock != seed.SafetyStock)
            {
                existing.SafetyStock = seed.SafetyStock;
                changed = true;
            }

            if (existing.WasteThreshold != seed.WasteThreshold)
            {
                existing.WasteThreshold = seed.WasteThreshold;
                changed = true;
            }

            if (existing.ShelfLifeDays != seed.ShelfLifeDays)
            {
                existing.ShelfLifeDays = seed.ShelfLifeDays;
                changed = true;
            }

            if (changed)
            {
                existing.UpdatedAt = now;
            }

            return;
        }

        db.Ingredients.Add(new Ingredient
        {
            Name = seed.Name,
            Unit = seed.Unit,
            Status = IngredientStatus.Active,
            Price = seed.Price,
            SafetyStock = seed.SafetyStock,
            WasteThreshold = seed.WasteThreshold,
            ShelfLifeDays = seed.ShelfLifeDays,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private static void EnsureProduct(AppDbContext db, SeedProduct seed)
    {
        var existing = db.Products.FirstOrDefault(x => x.Sku == seed.Sku);
        if (existing != null)
        {
            existing.Name = seed.Name;
            existing.Unit = seed.Unit;
            existing.Status = ProductStatus.Active;
            existing.ProductType = seed.ProductType;
            existing.ShelfLifeDays = seed.ShelfLifeDays;
            return;
        }

        db.Products.Add(new Product
        {
            Name = seed.Name,
            Sku = seed.Sku,
            Unit = seed.Unit,
            Status = ProductStatus.Active,
            ProductType = seed.ProductType,
            ShelfLifeDays = seed.ShelfLifeDays
        });
    }

    private static void EnsureStoreCatalog(
        AppDbContext db,
        int franchiseId,
        int productId,
        decimal price,
        DateTime now)
    {
        var existing = db.StoreCatalogs.FirstOrDefault(x =>
            x.FranchiseId == franchiseId &&
            x.ProductId == productId);

        if (existing != null)
        {
            var changed = false;

            if (existing.Price != price)
            {
                existing.Price = price;
                changed = true;
            }

            if (!string.Equals(existing.Status, StoreCatalogStatus.Active, StringComparison.OrdinalIgnoreCase))
            {
                existing.Status = StoreCatalogStatus.Active;
                changed = true;
            }

            if (changed)
            {
                existing.UpdatedAt = now;
            }

            return;
        }

        db.StoreCatalogs.Add(new StoreCatalog
        {
            FranchiseId = franchiseId,
            ProductId = productId,
            Price = price,
            Status = StoreCatalogStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    // ==================================================
    // 7) Recipe + BOM
    // ==================================================
    private static void SeedRecipesAndBoms(AppDbContext db, DateTime now)
    {
        Ingredient I(string name) => db.Ingredients.First(x => x.Name == name);
        Product P(string sku) => db.Products.First(x => x.Sku == sku);

        EnsureRecipe(
            db,
            P("SF-BSS-001").ProductId,
            1,
            BomStatus.Active,
            """
            1) Mix brown sugar with water.
            2) Heat until dissolved and slightly thickened.
            3) Cool down and store chilled.
            """,
            now);

        EnsureBom(
            db,
            P("SF-BSS-001").ProductId,
            1,
            BomStatus.Active,
            now,
            new[]
            {
                new BomSeedItem(I("Brown Sugar").IngredientId, 400m),
                new BomSeedItem(I("Water").IngredientId, 600m),
            });

        EnsureRecipe(
            db,
            P("SF-BT-001").ProductId,
            1,
            BomStatus.Active,
            """
            1) Brew black tea leaves with hot water.
            2) Steep for 10-12 minutes.
            3) Filter and cool. Store chilled.
            """,
            now);

        EnsureBom(
            db,
            P("SF-BT-001").ProductId,
            1,
            BomStatus.Active,
            now,
            new[]
            {
                new BomSeedItem(I("Black Tea Leaves").IngredientId, 120m),
                new BomSeedItem(I("Water").IngredientId, 3000m),
            });

        EnsureRecipe(
            db,
            P("SF-PEARL-001").ProductId,
            1,
            BomStatus.Active,
            """
            1) Boil water.
            2) Cook dry tapioca pearls for 20-25 minutes.
            3) Rest and rinse.
            4) Soak with brown sugar.
            """,
            now);

        EnsureBom(
            db,
            P("SF-PEARL-001").ProductId,
            1,
            BomStatus.Active,
            now,
            new[]
            {
                new BomSeedItem(I("Tapioca Pearls (Dry)").IngredientId, 800m),
                new BomSeedItem(I("Water").IngredientId, 4000m),
                new BomSeedItem(I("Brown Sugar").IngredientId, 300m),
            });

        EnsureRecipe(
            db,
            P("FT-CLMT-500").ProductId,
            1,
            BomStatus.Active,
            """
            1) Add tea base.
            2) Add milk/creamer.
            3) Add sugar syrup.
            4) Add ice.
            5) Shake and serve.
            """,
            now);

        EnsureBom(
            db,
            P("FT-CLMT-500").ProductId,
            1,
            BomStatus.Active,
            now,
            new[]
            {
                new BomSeedItem(I("Black Tea Leaves").IngredientId, 12m),
                new BomSeedItem(I("Water").IngredientId, 300m),
                new BomSeedItem(I("Milk Powder").IngredientId, 25m),
                new BomSeedItem(I("Sugar Syrup").IngredientId, 30m),
                new BomSeedItem(I("Ice Cubes").IngredientId, 180m),
                new BomSeedItem(I("Cup 500ml").IngredientId, 1m),
                new BomSeedItem(I("Lid 500ml").IngredientId, 1m),
                new BomSeedItem(I("Straw").IngredientId, 1m),
            });

        EnsureRecipe(
            db,
            P("FT-BSMT-500").ProductId,
            1,
            BomStatus.Active,
            """
            1) Add brown sugar syrup to cup wall.
            2) Add milk base.
            3) Add cooked pearls.
            4) Add ice and serve.
            """,
            now);

        EnsureBom(
            db,
            P("FT-BSMT-500").ProductId,
            1,
            BomStatus.Active,
            now,
            new[]
            {
                new BomSeedItem(I("Brown Sugar").IngredientId, 25m),
                new BomSeedItem(I("Milk Powder").IngredientId, 30m),
                new BomSeedItem(I("Water").IngredientId, 250m),
                new BomSeedItem(I("Tapioca Pearls (Dry)").IngredientId, 40m),
                new BomSeedItem(I("Ice Cubes").IngredientId, 180m),
                new BomSeedItem(I("Cup 500ml").IngredientId, 1m),
                new BomSeedItem(I("Lid 500ml").IngredientId, 1m),
                new BomSeedItem(I("Straw").IngredientId, 1m),
            });

        EnsureRecipe(
            db,
            P("FT-TARO-500").ProductId,
            1,
            BomStatus.Active,
            """
            1) Mix taro powder with water.
            2) Add milk base.
            3) Add sugar syrup if needed.
            4) Add ice and shake.
            """,
            now);

        EnsureBom(
            db,
            P("FT-TARO-500").ProductId,
            1,
            BomStatus.Active,
            now,
            new[]
            {
                new BomSeedItem(I("Taro Powder").IngredientId, 35m),
                new BomSeedItem(I("Milk Powder").IngredientId, 25m),
                new BomSeedItem(I("Water").IngredientId, 280m),
                new BomSeedItem(I("Sugar Syrup").IngredientId, 20m),
                new BomSeedItem(I("Ice Cubes").IngredientId, 180m),
                new BomSeedItem(I("Cup 500ml").IngredientId, 1m),
                new BomSeedItem(I("Lid 500ml").IngredientId, 1m),
                new BomSeedItem(I("Straw").IngredientId, 1m),
            });

        db.SaveChanges();
    }

    private static void EnsureRecipe(
        AppDbContext db,
        int productId,
        int version,
        string status,
        string instructions,
        DateTime now)
    {
        var existing = db.Recipes.FirstOrDefault(x =>
            x.ProductId == productId &&
            x.Version == version);

        if (existing != null)
        {
            existing.Status = status;
            existing.Instructions = instructions;
            existing.UpdatedAt = now;
            return;
        }

        db.Recipes.Add(new Recipe
        {
            ProductId = productId,
            Version = version,
            Status = status,
            Instructions = instructions,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private static void EnsureBom(
        AppDbContext db,
        int productId,
        int version,
        string status,
        DateTime now,
        IReadOnlyCollection<BomSeedItem> items)
    {
        var bom = db.Boms
            .Include(x => x.Items)
            .FirstOrDefault(x => x.ProductId == productId && x.Version == version);

        if (bom == null)
        {
            bom = new Bom
            {
                ProductId = productId,
                Version = version,
                Status = status,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.Boms.Add(bom);
            db.SaveChanges();
            db.Entry(bom).Collection(x => x.Items).Load();
        }
        else
        {
            bom.Status = status;
            bom.UpdatedAt = now;
        }

        var byIngredientId = bom.Items.ToDictionary(x => x.IngredientId, x => x);
        var incomingIds = items.Select(x => x.IngredientId).ToHashSet();

        foreach (var item in items)
        {
            if (byIngredientId.TryGetValue(item.IngredientId, out var existingItem))
            {
                if (existingItem.Quantity != item.Quantity)
                {
                    existingItem.Quantity = item.Quantity;
                }
            }
            else
            {
                db.BomItems.Add(new BomItem
                {
                    BomId = bom.BomId,
                    IngredientId = item.IngredientId,
                    Quantity = item.Quantity
                });
            }
        }

        var staleItems = bom.Items
            .Where(x => !incomingIds.Contains(x.IngredientId))
            .ToList();

        if (staleItems.Count > 0)
        {
            db.BomItems.RemoveRange(staleItems);
        }
    }

    // ==================================================
    // 8) CentralKitchen ingredient inventory (derived expiry)
    // ==================================================
    private static void SeedCentralKitchenIngredientInventory(AppDbContext db, DateTime now)
    {
        var ck = db.CentralKitchens.First(x => x.Name == CentralKitchenName);

        Ingredient I(string name) => db.Ingredients.First(x => x.Name == name);

        EnsureIngredientBatch(
            db,
            ingredientId: I("Black Tea Leaves").IngredientId,
            ownerType: InventoryOwnerType.CentralKitchen,
            franchiseId: null,
            centralKitchenId: ck.CentralKitchenId,
            batchCode: "CK-ING-BTL-001",
            quantity: 10000m,
            createdAt: CreatedAtFromTargetExpiry(now.AddMonths(12), I("Black Tea Leaves").ShelfLifeDays));

        EnsureIngredientBatch(
            db,
            ingredientId: I("Milk Powder").IngredientId,
            ownerType: InventoryOwnerType.CentralKitchen,
            franchiseId: null,
            centralKitchenId: ck.CentralKitchenId,
            batchCode: "CK-ING-MP-001",
            quantity: 8000m,
            createdAt: CreatedAtFromTargetExpiry(now.AddMonths(9), I("Milk Powder").ShelfLifeDays));

        EnsureIngredientBatch(
            db,
            ingredientId: I("Non-dairy Creamer").IngredientId,
            ownerType: InventoryOwnerType.CentralKitchen,
            franchiseId: null,
            centralKitchenId: ck.CentralKitchenId,
            batchCode: "CK-ING-NDC-001",
            quantity: 8000m,
            createdAt: CreatedAtFromTargetExpiry(now.AddMonths(9), I("Non-dairy Creamer").ShelfLifeDays));

        EnsureIngredientBatch(
            db,
            ingredientId: I("Sugar Syrup").IngredientId,
            ownerType: InventoryOwnerType.CentralKitchen,
            franchiseId: null,
            centralKitchenId: ck.CentralKitchenId,
            batchCode: "CK-ING-SS-001",
            quantity: 12000m,
            createdAt: CreatedAtFromTargetExpiry(now.AddMonths(4), I("Sugar Syrup").ShelfLifeDays));

        EnsureIngredientBatch(
            db,
            ingredientId: I("Brown Sugar").IngredientId,
            ownerType: InventoryOwnerType.CentralKitchen,
            franchiseId: null,
            centralKitchenId: ck.CentralKitchenId,
            batchCode: "CK-ING-BS-001",
            quantity: 6000m,
            createdAt: CreatedAtFromTargetExpiry(now.AddMonths(12), I("Brown Sugar").ShelfLifeDays));

        EnsureIngredientBatch(
            db,
            ingredientId: I("Tapioca Pearls (Dry)").IngredientId,
            ownerType: InventoryOwnerType.CentralKitchen,
            franchiseId: null,
            centralKitchenId: ck.CentralKitchenId,
            batchCode: "CK-ING-TP-001",
            quantity: 7000m,
            createdAt: CreatedAtFromTargetExpiry(now.AddMonths(8), I("Tapioca Pearls (Dry)").ShelfLifeDays));

        EnsureIngredientBatch(
            db,
            ingredientId: I("Taro Powder").IngredientId,
            ownerType: InventoryOwnerType.CentralKitchen,
            franchiseId: null,
            centralKitchenId: ck.CentralKitchenId,
            batchCode: "CK-ING-TARO-001",
            quantity: 4000m,
            createdAt: CreatedAtFromTargetExpiry(now.AddMonths(8), I("Taro Powder").ShelfLifeDays));

        EnsureIngredientBatch(
            db,
            ingredientId: I("Water").IngredientId,
            ownerType: InventoryOwnerType.CentralKitchen,
            franchiseId: null,
            centralKitchenId: ck.CentralKitchenId,
            batchCode: "CK-ING-WATER-001",
            quantity: 100000m,
            createdAt: CreatedAtFromTargetExpiry(now.AddYears(1), I("Water").ShelfLifeDays));

        EnsureIngredientBatch(
            db,
            ingredientId: I("Ice Cubes").IngredientId,
            ownerType: InventoryOwnerType.CentralKitchen,
            franchiseId: null,
            centralKitchenId: ck.CentralKitchenId,
            batchCode: "CK-ING-ICE-001",
            quantity: 30000m,
            createdAt: CreatedAtFromTargetExpiry(now.AddDays(7), I("Ice Cubes").ShelfLifeDays));

        EnsureIngredientBatch(
            db,
            ingredientId: I("Cup 500ml").IngredientId,
            ownerType: InventoryOwnerType.CentralKitchen,
            franchiseId: null,
            centralKitchenId: ck.CentralKitchenId,
            batchCode: "CK-ING-CUP-001",
            quantity: 1000m,
            createdAt: CreatedAtFromTargetExpiry(now.AddYears(3), I("Cup 500ml").ShelfLifeDays));

        EnsureIngredientBatch(
            db,
            ingredientId: I("Lid 500ml").IngredientId,
            ownerType: InventoryOwnerType.CentralKitchen,
            franchiseId: null,
            centralKitchenId: ck.CentralKitchenId,
            batchCode: "CK-ING-LID-001",
            quantity: 1000m,
            createdAt: CreatedAtFromTargetExpiry(now.AddYears(3), I("Lid 500ml").ShelfLifeDays));

        EnsureIngredientBatch(
            db,
            ingredientId: I("Straw").IngredientId,
            ownerType: InventoryOwnerType.CentralKitchen,
            franchiseId: null,
            centralKitchenId: ck.CentralKitchenId,
            batchCode: "CK-ING-STRAW-001",
            quantity: 1000m,
            createdAt: CreatedAtFromTargetExpiry(now.AddYears(3), I("Straw").ShelfLifeDays));

        db.SaveChanges();
    }

    private static void EnsureIngredientBatch(
        AppDbContext db,
        int ingredientId,
        string ownerType,
        int? franchiseId,
        int? centralKitchenId,
        string batchCode,
        decimal quantity,
        DateTime createdAt)
    {
        var existing = db.IngredientBatches.FirstOrDefault(x => x.BatchCode == batchCode);
        if (existing != null)
        {
            existing.IngredientId = ingredientId;
            existing.Type = ownerType;
            existing.FranchiseId = franchiseId;
            existing.CentralKitchenId = centralKitchenId;
            existing.Quantity = quantity;
            existing.CreatedAt = createdAt;
            return;
        }

        db.IngredientBatches.Add(new IngredientBatch
        {
            IngredientId = ingredientId,
            Type = ownerType,
            FranchiseId = franchiseId,
            CentralKitchenId = centralKitchenId,
            BatchCode = batchCode,
            Quantity = quantity,
            CreatedAt = createdAt
        });
    }

    // ==================================================
    // Seed DTOs
    // ==================================================
    private sealed record SeedIngredient(
        string Name,
        string Unit,
        decimal Price,
        decimal SafetyStock,
        decimal WasteThreshold,
        int ShelfLifeDays);

    private sealed record SeedProduct(
        string Name,
        string Sku,
        string Unit,
        string ProductType,
        int ShelfLifeDays);

    private sealed record BomSeedItem(
        int IngredientId,
        decimal Quantity);

    private sealed record StoreOrderSeedItem(
        int ProductId,
        decimal Quantity);

    private sealed record DemandAggregationSeedItem(
        int ProductId,
        decimal Quantity);

    // ==================================================
    // 9) Store Order
    // ==================================================
    private static void SeedStoreOrders(AppDbContext db, DateTime now)
    {
        var frQ1 = db.Franchises.First(x => x.Name == FranchiseQ1Name);
        var frQ7 = db.Franchises.First(x => x.Name == FranchiseQ7Name);

        Product P(string sku) => db.Products.First(x => x.Sku == sku);

        EnsureStoreOrder(
            db,
            franchiseId: frQ1.FranchiseId,
            orderDate: DateOnly.FromDateTime(now.Date.AddDays(1)),
            status: StoreOrderStatus.Submitted,
            createdAt: now.AddHours(-8),
            submittedAt: now.AddHours(-7),
            items: new[]
            {
                new StoreOrderSeedItem(P("FT-CLMT-500").ProductId, 35m),
                new StoreOrderSeedItem(P("FT-BSMT-500").ProductId, 28m),
                new StoreOrderSeedItem(P("FT-TARO-500").ProductId, 18m),
            });

        EnsureStoreOrder(
            db,
            franchiseId: frQ7.FranchiseId,
            orderDate: DateOnly.FromDateTime(now.Date.AddDays(1)),
            status: StoreOrderStatus.Submitted,
            createdAt: now.AddHours(-6),
            submittedAt: now.AddHours(-5),
            items: new[]
            {
                new StoreOrderSeedItem(P("FT-CLMT-500").ProductId, 30m),
                new StoreOrderSeedItem(P("FT-BSMT-500").ProductId, 24m),
                new StoreOrderSeedItem(P("FT-TARO-500").ProductId, 20m),
            });

        EnsureStoreOrder(
            db,
            franchiseId: frQ1.FranchiseId,
            orderDate: DateOnly.FromDateTime(now.Date.AddDays(2)),
            status: StoreOrderStatus.Draft,
            createdAt: now.AddHours(-2),
            submittedAt: null,
            items: new[]
            {
                new StoreOrderSeedItem(P("FT-CLMT-500").ProductId, 20m),
                new StoreOrderSeedItem(P("FT-BSMT-500").ProductId, 16m),
            });

        db.SaveChanges();
    }

    private static void EnsureStoreOrder(
        AppDbContext db,
        int franchiseId,
        DateOnly orderDate,
        string status,
        DateTime createdAt,
        DateTime? submittedAt,
        IReadOnlyCollection<StoreOrderSeedItem> items)
    {
        var order = db.StoreOrders
            .Include(x => x.Items)
            .FirstOrDefault(x =>
                x.FranchiseId == franchiseId &&
                x.OrderDate == orderDate &&
                x.Status == status);

        if (order == null)
        {
            order = new StoreOrder
            {
                FranchiseId = franchiseId,
                OrderDate = orderDate,
                Status = status,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                SubmittedAt = submittedAt
            };

            db.StoreOrders.Add(order);
            db.SaveChanges();
            db.Entry(order).Collection(x => x.Items).Load();
        }
        else
        {
            order.Status = status;
            order.SubmittedAt = submittedAt;
            order.UpdatedAt = DateTime.UtcNow;
        }

        var existingItems = order.Items.ToDictionary(x => x.ProductId, x => x);
        var incomingIds = items.Select(x => x.ProductId).ToHashSet();

        foreach (var item in items)
        {
            if (existingItems.TryGetValue(item.ProductId, out var existing))
            {
                existing.Quantity = item.Quantity;
            }
            else
            {
                db.StoreOrderItems.Add(new StoreOrderItem
                {
                    StoreOrderId = order.StoreOrderId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                });
            }
        }

        var stale = order.Items.Where(x => !incomingIds.Contains(x.ProductId)).ToList();
        if (stale.Count > 0)
        {
            db.StoreOrderItems.RemoveRange(stale);
        }
    }

    // ==================================================
    // 10) Demand Aggregations
    // ==================================================
    private static void SeedDemandAggregations(AppDbContext db, DateTime now)
    {
        var targetDates = db.StoreOrders
            .Where(x => x.Status == StoreOrderStatus.Submitted)
            .Select(x => x.OrderDate)
            .Distinct()
            .ToList();

        foreach (var planDate in targetDates)
        {
            var demandByProduct = db.StoreOrderItems
                .Where(i => i.StoreOrder.Status == StoreOrderStatus.Submitted &&
                            i.StoreOrder.OrderDate == planDate)
                .GroupBy(i => i.ProductId)
                .Select(g => new DemandAggregationSeedItem(
                    g.Key,
                    g.Sum(x => x.Quantity)))
                .ToList();

            EnsureDemandAggregation(db, planDate, now, demandByProduct);
        }

        db.SaveChanges();
    }

    private static void EnsureDemandAggregation(
        AppDbContext db,
        DateOnly planDate,
        DateTime now,
        IReadOnlyCollection<DemandAggregationSeedItem> demandItems)
    {
        var aggregation = db.DemandAggregations
            .Include(x => x.DemandItems)
            .FirstOrDefault(x => x.PlanDate == planDate);

        if (aggregation == null)
        {
            aggregation = new DemandAggregation
            {
                PlanDate = planDate,
                CreatedAt = now
            };

            db.DemandAggregations.Add(aggregation);
            db.SaveChanges();
            db.Entry(aggregation).Collection(x => x.DemandItems).Load();
        }

        var existingItems = aggregation.DemandItems.ToDictionary(x => x.ProductId, x => x);
        var incomingIds = demandItems.Select(x => x.ProductId).ToHashSet();

        foreach (var item in demandItems)
        {
            if (existingItems.TryGetValue(item.ProductId, out var existing))
            {
                existing.Quantity = item.Quantity;
            }
            else
            {
                db.DemandItems.Add(new DemandItem
                {
                    DemandAggregationId = aggregation.DemandAggregationId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                });
            }
        }

        var stale = aggregation.DemandItems
            .Where(x => !incomingIds.Contains(x.ProductId))
            .ToList();

        if (stale.Count > 0)
        {
            db.DemandItems.RemoveRange(stale);
        }
    }

    // ==================================================
    // 11) Production Plans + Production Runs
    // ==================================================
    private static void SeedProductionPlansAndRuns(AppDbContext db, DateTime now)
    {
        var ck = db.CentralKitchens.First(x => x.Name == CentralKitchenName);

        var aggregations = db.DemandAggregations
            .Include(x => x.DemandItems)
            .OrderBy(x => x.PlanDate)
            .ToList();

        foreach (var aggregation in aggregations)
        {
            var plan = EnsureProductionPlan(db, ck.CentralKitchenId, aggregation.PlanDate, now);

            foreach (var demand in aggregation.DemandItems)
            {
                EnsureProductionPlanItem(
                    db,
                    productionPlanId: plan.ProductionPlanId,
                    productId: demand.ProductId,
                    quantity: demand.Quantity);
            }

            var totalQty = aggregation.DemandItems.Sum(x => x.Quantity);

            EnsureProductionRun(
                db,
                productionPlanId: plan.ProductionPlanId,
                centralKitchenId: ck.CentralKitchenId,
                runCode: $"RUN-{aggregation.PlanDate:yyyyMMdd}-001",
                productionDate: aggregation.PlanDate,
                quantity: totalQty,
                status: ProductionRunStatuses.Completed,
                completedAt: UtcAtStartOfDay(aggregation.PlanDate).AddHours(8));
        }

        db.SaveChanges();
    }

    private static ProductionPlan EnsureProductionPlan(
        AppDbContext db,
        int centralKitchenId,
        DateOnly planDate,
        DateTime now)
    {
        var existing = db.ProductionPlans
            .FirstOrDefault(x => x.CentralKitchenId == centralKitchenId && x.PlanDate == planDate);

        if (existing != null)
        {
            if (existing.Status == null)
            {
                existing.Status = ProductionPlanStatus.DRAFT;
            }

            return existing;
        }

        var created = new ProductionPlan
        {
            CentralKitchenId = centralKitchenId,
            PlanDate = planDate,
            Status = ProductionPlanStatus.DRAFT,
            CreatedAt = now,
            UpdateAt = now
        };

        db.ProductionPlans.Add(created);
        db.SaveChanges();
        return created;
    }

    private static void EnsureProductionPlanItem(
        AppDbContext db,
        int productionPlanId,
        int productId,
        decimal quantity)
    {
        var existing = db.ProductionPlanItems
            .FirstOrDefault(x => x.ProductionPlanId == productionPlanId && x.ProductId == productId);

        if (existing != null)
        {
            existing.Quantity = quantity;
            return;
        }

        db.ProductionPlanItems.Add(new ProductionPlanItem
        {
            ProductionPlanId = productionPlanId,
            ProductId = productId,
            Quantity = quantity
        });
    }

    private static void EnsureProductionRun(
        AppDbContext db,
        int productionPlanId,
        int centralKitchenId,
        string runCode,
        DateOnly productionDate,
        decimal quantity,
        string status,
        DateTime? completedAt)
    {
        var existing = db.ProductionRuns.FirstOrDefault(x => x.RunCode == runCode);
        if (existing != null)
        {
            existing.ProductionPlanId = productionPlanId;
            existing.CentralKitchenId = centralKitchenId;
            existing.ProductionDate = productionDate;
            existing.Quantity = quantity;
            existing.Status = status;
            existing.CompletedAt = completedAt;
            return;
        }

        db.ProductionRuns.Add(new ProductionRun
        {
            ProductionPlanId = productionPlanId,
            CentralKitchenId = centralKitchenId,
            RunCode = runCode,
            ProductionDate = productionDate,
            Quantity = quantity,
            Status = status,
            CreatedAt = completedAt ?? DateTime.UtcNow,
            CompletedAt = completedAt
        });
    }

    // ==================================================
    // 12) ProductBatch (derived expiry, CK-owned)
    // ==================================================
    private static void SeedCentralKitchenProductInventory(AppDbContext db, DateTime now)
    {
        var ck = db.CentralKitchens.First(x => x.Name == CentralKitchenName);

        var completedRuns = db.ProductionRuns
            .Where(x => x.Status == ProductionRunStatuses.Completed)
            .OrderBy(x => x.ProductionDate)
            .ToList();

        foreach (var run in completedRuns)
        {
            var planItems = db.ProductionPlanItems
                .Where(x => x.ProductionPlanId == run.ProductionPlanId)
                .ToList();

            foreach (var item in planItems)
            {
                var batchCode = $"PB-{run.RunCode}-{item.ProductId}";
                var createdAt = run.CompletedAt ?? UtcAtStartOfDay(run.ProductionDate).AddHours(8);

                EnsureProductBatch(
                    db,
                    productId: item.ProductId,
                    productionRunId: run.ProductionRunId,
                    franchiseId: null,
                    centralKitchenId: ck.CentralKitchenId,
                    batchCode: batchCode,
                    quantity: item.Quantity,
                    createdAt: createdAt);
            }
        }

        db.SaveChanges();
    }

    private static void EnsureProductBatch(
        AppDbContext db,
        int productId,
        int? productionRunId,
        int? franchiseId,
        int? centralKitchenId,
        string batchCode,
        decimal quantity,
        DateTime createdAt)
    {
        var existing = db.ProductBatches.FirstOrDefault(x => x.BatchCode == batchCode);
        if (existing != null)
        {
            existing.ProductId = productId;
            existing.ProductionRunId = productionRunId;
            existing.FranchiseId = franchiseId;
            existing.CentralKitchenId = centralKitchenId;
            existing.Quantity = quantity;
            existing.CreatedAt = createdAt;
            return;
        }

        db.ProductBatches.Add(new ProductBatch
        {
            ProductId = productId,
            ProductionRunId = productionRunId,
            FranchiseId = franchiseId,
            CentralKitchenId = centralKitchenId,
            BatchCode = batchCode,
            Quantity = quantity,
            CreatedAt = createdAt
        });
    }

    // ==================================================
    // Helpers
    // ==================================================
    private static DateTime CreatedAtFromTargetExpiry(DateTime targetExpiryUtc, int shelfLifeDays)
    {
        if (shelfLifeDays <= 0)
            throw new InvalidOperationException("ShelfLifeDays must be > 0 for derived expiry seeding.");

        return DateTime.SpecifyKind(targetExpiryUtc.Date, DateTimeKind.Utc).AddDays(-shelfLifeDays);
    }

    private static DateTime UtcAtStartOfDay(DateOnly date)
    {
        return DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    }
}