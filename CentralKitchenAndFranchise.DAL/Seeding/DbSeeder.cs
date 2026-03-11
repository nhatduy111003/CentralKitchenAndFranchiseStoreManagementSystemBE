using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.DAL.Seeding;

public static class DbSeeder
{
    // ===== Default Accounts (change if needed) =====
    private const string DefaultPassword = "123456";

    private const string AdminUsername = "admin";
    private const string AdminEmail = "admin@gmail.com";

    private const string ManagerUsername = "manager";
    private const string ManagerEmail = "manager@gmail.com";

    private const string SupplyUsername = "supply";
    private const string SupplyEmail = "supply@gmail.com";

    private const string KitchenUsername = "kitchen";
    private const string KitchenEmail = "kitchen@gmail.com";

    private const string StoreQ1Username = "store.q1";
    private const string StoreQ1Email = "store.q1@gmail.com";

    private const string StoreQ7Username = "store.q7";
    private const string StoreQ7Email = "store.q7@gmail.com";

    public static void Seed(AppDbContext db)
    {
        // IMPORTANT: keep seed idempotent; never assume empty DB
        SeedRoles(db);
        SeedFranchises(db);
        SeedUsers(db);
        SeedSystemSettings(db);

        SeedMilkTeaMasterData(db); // ingredients + products + store catalog
        SeedMilkTeaBomAndRecipe(db);

        db.SaveChanges();
    }

    // ===== Roles =====
    private static void SeedRoles(AppDbContext db)
    {
        var required = new[]
        {
            RoleNames.Admin,
            RoleNames.Manager,
            RoleNames.SupplyCoordinator,
            RoleNames.KitchenStaff,
            RoleNames.StoreStaff
        };

        var existing = db.Roles
            .Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = false;

        foreach (var name in required)
        {
            if (existing.Contains(name)) continue;
            db.Roles.Add(new Role { Name = name });
            added = true;
        }

        if (added) db.SaveChanges();
    }

    // ===== Franchises =====
    private static void SeedFranchises(AppDbContext db)
    {
        var now = DateTime.UtcNow;

        EnsureFranchise(
            db,
            name: "Central Kitchen - HCMC",
            type: "CENTRAL_KITCHEN",
            address: "Warehouse 01, Tan Binh, HCMC",
            location: "Ho Chi Minh City",
            lat: 10.8019,
            lng: 106.6522,
            now: now);

        EnsureFranchise(
            db,
            name: "Franchise Store - District 1",
            type: "FRANCHISE",
            address: "Nguyen Hue, District 1, HCMC",
            location: "Ho Chi Minh City",
            lat: 10.7756,
            lng: 106.7033,
            now: now);

        EnsureFranchise(
            db,
            name: "Franchise Store - District 7",
            type: "FRANCHISE",
            address: "Phu My Hung, District 7, HCMC",
            location: "Ho Chi Minh City",
            lat: 10.7290,
            lng: 106.7218,
            now: now);

        db.SaveChanges();
    }

    private static Franchise EnsureFranchise(
        AppDbContext db,
        string name,
        string type,
        string? address,
        string? location,
        double? lat,
        double? lng,
        DateTime now)
    {
        var existing = db.Franchises.FirstOrDefault(x => x.Name.ToLower() == name.ToLower());
        if (existing != null)
        {
            var changed = false;

            if (!string.Equals(existing.Type, type, StringComparison.OrdinalIgnoreCase)) { existing.Type = type; changed = true; }
            if (!string.Equals(existing.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)) { existing.Status = "ACTIVE"; changed = true; }
            if (existing.Address != address) { existing.Address = address; changed = true; }
            if (existing.Location != location) { existing.Location = location; changed = true; }
            if (existing.Latitude != lat) { existing.Latitude = lat; changed = true; }
            if (existing.Longitude != lng) { existing.Longitude = lng; changed = true; }

            if (changed) existing.UpdatedAt = now;

            return existing;
        }

        var fr = new Franchise
        {
            Name = name,
            Type = type,
            Status = "ACTIVE",
            Address = address,
            Location = location,
            Latitude = lat,
            Longitude = lng,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Franchises.Add(fr);
        return fr;
    }

    // ===== Users + UserWorkAssignment =====
    private static void SeedUsers(AppDbContext db)
    {
        var now = DateTime.UtcNow;

        var adminRoleId = GetRoleId(db, RoleNames.Admin);
        var managerRoleId = GetRoleId(db, RoleNames.Manager);
        var supplyRoleId = GetRoleId(db, RoleNames.SupplyCoordinator);
        var kitchenRoleId = GetRoleId(db, RoleNames.KitchenStaff);
        var storeRoleId = GetRoleId(db, RoleNames.StoreStaff);

        var admin = EnsureUser(db, AdminUsername, AdminEmail, adminRoleId, now);
        var manager = EnsureUser(db, ManagerUsername, ManagerEmail, managerRoleId, now);
        var supply = EnsureUser(db, SupplyUsername, SupplyEmail, supplyRoleId, now);
        var kitchen = EnsureUser(db, KitchenUsername, KitchenEmail, kitchenRoleId, now);

        // Store staff should be assigned to specific franchise (RBAC scope)
        var frQ1 = db.Franchises.First(x => x.Name == "Franchise Store - District 1");
        var frQ7 = db.Franchises.First(x => x.Name == "Franchise Store - District 7");

        var storeQ1 = EnsureUser(db, StoreQ1Username, StoreQ1Email, storeRoleId, now);
        var storeQ7 = EnsureUser(db, StoreQ7Username, StoreQ7Email, storeRoleId, now);

        // Assign OU-scoped users by UserWorkAssignment
        EnsureUserWorkAssignment(
            db,
            storeQ1.UserId,
            WorkAssignmentTypes.Franchise,
            frQ1.FranchiseId,
            null,
            now);

        EnsureUserWorkAssignment(
            db,
            storeQ7.UserId,
            WorkAssignmentTypes.Franchise,
            frQ7.FranchiseId,
            null,
            now);

        // Supply / kitchen staff belong to central kitchen scope
        var centralKitchen = db.CentralKitchens.First(x => x.Name == "Central Kitchen - HCMC");

        EnsureUserWorkAssignment(
            db,
            supply.UserId,
            WorkAssignmentTypes.CentralKitchen,
            null,
            centralKitchen.CentralKitchenId,
            now);

        EnsureUserWorkAssignment(
            db,
            kitchen.UserId,
            WorkAssignmentTypes.CentralKitchen,
            null,
            centralKitchen.CentralKitchenId,
            now);

        // Admin/Manager are global by design -> no assignment required
        db.SaveChanges();
    }

    private static int GetRoleId(AppDbContext db, string roleName)
    {
        var roleId = db.Roles.Where(r => r.Name == roleName).Select(r => r.RoleId).FirstOrDefault();
        if (roleId == 0) throw new InvalidOperationException($"Role not found: {roleName}. SeedRoles failed?");
        return roleId;
    }

    private static User EnsureUser(AppDbContext db, string username, string email, int roleId, DateTime now)
    {
        var keyU = username.ToLower();
        var keyE = email.ToLower();

        var user = db.Users.FirstOrDefault(u =>
            u.Username.ToLower() == keyU || u.Email.ToLower() == keyE);

        if (user == null)
        {
            user = new User
            {
                Username = username,
                Email = email,
                RoleId = roleId,
                Status = "ACTIVE",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Users.Add(user);
            db.SaveChanges(); // need UserId
            return user;
        }

        var changed = false;

        if (user.RoleId != roleId) { user.RoleId = roleId; changed = true; }
        if (!string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)) { user.Status = "ACTIVE"; changed = true; }
        if (string.IsNullOrWhiteSpace(user.PasswordHash)) { user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword); changed = true; }

        if (changed)
        {
            user.UpdatedAt = now;
            db.SaveChanges();
        }

        return user;
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

    // ===== System Settings =====
    private static void SeedSystemSettings(AppDbContext db)
    {
        var now = DateTime.UtcNow;

        void Ensure(string key, string value, string desc)
        {
            var exists = db.SystemSettings.Any(x => x.Key == key);
            if (exists) return;

            db.SystemSettings.Add(new SystemSetting
            {
                Key = key,
                Value = value,
                Description = desc,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // inventory/quality windows
        Ensure(SettingKeys.NearExpiryDays, "7", "Near-expiry definition window in days");

        // ordering
        Ensure(SettingKeys.FutureOrderLimitDays, "7", "Max future order creation window in days ");
        Ensure(SettingKeys.OrderEditWindowMinutes, "30", "Allowed edit window after submit in minutes ");
    }

    // ===== Milk Tea Master Data =====
    private static void SeedMilkTeaMasterData(AppDbContext db)
    {
        var now = DateTime.UtcNow;

        // -------- Ingredients (mô hình trà sữa) --------
        // Unit convention: g / ml / pcs
        var ing = new (string Name, string Unit, decimal Price)[]
        {
            ("Black Tea Leaves", "g", 0.06m),
            ("Oolong Tea Leaves", "g", 0.08m),
            ("Jasmine Green Tea Leaves", "g", 0.07m),

            ("Milk Powder", "g", 0.10m),
            ("Non-dairy Creamer", "g", 0.09m),

            ("Sugar Syrup", "ml", 0.02m),
            ("Brown Sugar Syrup", "ml", 0.03m),

            ("Tapioca Pearls (Dry)", "g", 0.04m),
            ("Taro Powder", "g", 0.12m),

            ("Water", "ml", 0.0005m),
            ("Ice Cubes", "g", 0.001m),

            ("Cup 500ml", "pcs", 0.20m),
            ("Lid 500ml", "pcs", 0.08m),
            ("Straw", "pcs", 0.03m),
        };

        foreach (var (name, unit, price) in ing)
            EnsureIngredient(db, name, unit, price, now);

        db.SaveChanges();

        // -------- Products --------
        var products = new (string Name, string Sku, string Unit, string ProductType)[]
        {
            // FINISHED drinks
            ("Classic Milk Tea 500ml", "FT-CLMT-500", "cup", "FINISHED"),
            ("Brown Sugar Milk Tea 500ml", "FT-BSMT-500", "cup", "FINISHED"),
            ("Taro Milk Tea 500ml", "FT-TARO-500", "cup", "FINISHED"),

            // SEMI-FINISHED (for central kitchen prep)
            ("Brown Sugar Syrup (Batch)", "SF-BSS-001", "ml", "SEMI_FINISHED"),
            ("Black Tea Concentrate (Batch)", "SF-BT-001", "ml", "SEMI_FINISHED"),
            ("Cooked Tapioca Pearls (Batch)", "SF-PEARL-001", "g", "SEMI_FINISHED"),
        };

        foreach (var (name, sku, unit, type) in products)
            EnsureProduct(db, name, sku, unit, type);

        db.SaveChanges();

        // -------- Store Catalog (assign finished products to stores with price) --------
        var frQ1 = db.Franchises.First(x => x.Name == "Franchise Store - District 1");
        var frQ7 = db.Franchises.First(x => x.Name == "Franchise Store - District 7");

        var pClassic = db.Products.First(x => x.Sku == "FT-CLMT-500");
        var pBrown = db.Products.First(x => x.Sku == "FT-BSMT-500");
        var pTaro = db.Products.First(x => x.Sku == "FT-TARO-500");

        EnsureStoreCatalog(db, frQ1.FranchiseId, pClassic.ProductId, 35000m, now);
        EnsureStoreCatalog(db, frQ1.FranchiseId, pBrown.ProductId, 42000m, now);
        EnsureStoreCatalog(db, frQ1.FranchiseId, pTaro.ProductId, 40000m, now);

        EnsureStoreCatalog(db, frQ7.FranchiseId, pClassic.ProductId, 34000m, now);
        EnsureStoreCatalog(db, frQ7.FranchiseId, pBrown.ProductId, 41000m, now);
        EnsureStoreCatalog(db, frQ7.FranchiseId, pTaro.ProductId, 39000m, now);
    }

    private static Ingredient EnsureIngredient(AppDbContext db, string name, string unit, decimal price, DateTime now)
    {
        var existing = db.Ingredients.FirstOrDefault(x => x.Name.ToLower() == name.ToLower());
        if (existing != null)
        {
            var changed = false;

            if (!string.Equals(existing.Unit, unit, StringComparison.OrdinalIgnoreCase)) { existing.Unit = unit; changed = true; }
            if (!string.Equals(existing.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)) { existing.Status = "ACTIVE"; changed = true; }
            if (existing.Price != price) { existing.Price = price; changed = true; }

            if (changed) existing.UpdatedAt = now;
            return existing;
        }

        var ing = new Ingredient
        {
            Name = name,
            Unit = unit,
            Status = "ACTIVE",
            Price = price,
            SafetyStock = 0,
            WasteThreshold = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Ingredients.Add(ing);
        return ing;
    }

    private static Product EnsureProduct(AppDbContext db, string name, string sku, string unit, string productType)
    {
        var existing = db.Products.FirstOrDefault(x => x.Sku == sku);
        if (existing != null)
        {
            var changed = false;
            if (existing.Name != name) { existing.Name = name; changed = true; }
            if (existing.Unit != unit) { existing.Unit = unit; changed = true; }
            if (!string.Equals(existing.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)) { existing.Status = "ACTIVE"; changed = true; }
            if (!string.Equals(existing.ProductType, productType, StringComparison.OrdinalIgnoreCase)) { existing.ProductType = productType; changed = true; }

            if (changed) db.Products.Update(existing);
            return existing;
        }

        var p = new Product
        {
            Name = name,
            Sku = sku,
            Unit = unit,
            Status = "ACTIVE",
            ProductType = productType
        };

        db.Products.Add(p);
        return p;
    }

    private static void EnsureStoreCatalog(AppDbContext db, int franchiseId, int productId, decimal price, DateTime now)
    {
        var existing = db.StoreCatalogs.FirstOrDefault(x => x.FranchiseId == franchiseId && x.ProductId == productId);
        if (existing != null)
        {
            var changed = false;

            if (existing.Price != price) { existing.Price = price; changed = true; }
            if (!string.Equals(existing.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)) { existing.Status = "ACTIVE"; changed = true; }

            if (changed) existing.UpdatedAt = now;
            return;
        }

        db.StoreCatalogs.Add(new StoreCatalog
        {
            FranchiseId = franchiseId,
            ProductId = productId,
            Price = price,
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    // ===== BOM + Recipe for Milk Tea =====
    private static void SeedMilkTeaBomAndRecipe(AppDbContext db)
    {
        var now = DateTime.UtcNow;

        // Ingredient map (by name)
        Ingredient I(string name) => db.Ingredients.First(x => x.Name == name);

        // Product map (by sku)
        Product P(string sku) => db.Products.First(x => x.Sku == sku);

        // ---------- SEMI-FINISHED BOM + Recipe ----------
        // Brown Sugar Syrup (Batch)
        EnsureRecipe(db, P("SF-BSS-001").ProductId, 1, "ACTIVE",
            "Brown sugar syrup batch:\n1) Mix brown sugar + water.\n2) Heat until dissolved.\n3) Cool down and store chilled.",
            now);
        EnsureBom(db, P("SF-BSS-001").ProductId, 1, "ACTIVE", now, new[]
        {
            (I("Brown Sugar Syrup").IngredientId, 1000m), // treat as output reference? Keep BOM consistent -> use actual ingredients:
        }, allowFallback: true, fallbackItems: new[]
        {
            (I("Water").IngredientId, 600m),
            (I("Brown Sugar Syrup").IngredientId, 400m) // If you later add "Brown Sugar" ingredient, replace this line
        });

        // Black Tea Concentrate (Batch)
        EnsureRecipe(db, P("SF-BT-001").ProductId, 1, "ACTIVE",
            "Black tea concentrate batch:\n1) Brew black tea leaves with hot water.\n2) Steep 10-12 minutes.\n3) Filter and cool. Store chilled.",
            now);
        EnsureBom(db, P("SF-BT-001").ProductId, 1, "ACTIVE", now, new[]
        {
            (I("Black Tea Leaves").IngredientId, 120m),
            (I("Water").IngredientId, 3000m),
        });

        // Cooked Tapioca Pearls (Batch)
        EnsureRecipe(db, P("SF-PEARL-001").ProductId, 1, "ACTIVE",
            "Cooked tapioca pearls batch:\n1) Boil water.\n2) Cook dry pearls 20-25 minutes.\n3) Rest 20 minutes.\n4) Rinse and soak in brown sugar syrup.",
            now);
        EnsureBom(db, P("SF-PEARL-001").ProductId, 1, "ACTIVE", now, new[]
        {
            (I("Tapioca Pearls (Dry)").IngredientId, 800m),
            (I("Water").IngredientId, 4000m),
            (I("Brown Sugar Syrup").IngredientId, 300m),
        });

        // ---------- FINISHED DRINKS BOM + Recipe ----------
        // Classic Milk Tea 500ml
        EnsureRecipe(db, P("FT-CLMT-500").ProductId, 1, "ACTIVE",
            "Classic Milk Tea (500ml):\n1) Add tea base.\n2) Add milk/creamer.\n3) Add sugar syrup.\n4) Add ice.\n5) Shake and serve.",
            now);
        EnsureBom(db, P("FT-CLMT-500").ProductId, 1, "ACTIVE", now, new[]
        {
            (I("Black Tea Leaves").IngredientId, 12m),
            (I("Water").IngredientId, 300m),
            (I("Milk Powder").IngredientId, 25m),
            (I("Sugar Syrup").IngredientId, 30m),
            (I("Ice Cubes").IngredientId, 180m),
            (I("Cup 500ml").IngredientId, 1m),
            (I("Lid 500ml").IngredientId, 1m),
            (I("Straw").IngredientId, 1m),
        });

        // Brown Sugar Milk Tea 500ml (with pearls)
        EnsureRecipe(db, P("FT-BSMT-500").ProductId, 1, "ACTIVE",
            "Brown Sugar Milk Tea (500ml):\n1) Add brown sugar syrup to cup wall.\n2) Add milk base.\n3) Add cooked pearls.\n4) Add ice and top.",
            now);
        EnsureBom(db, P("FT-BSMT-500").ProductId, 1, "ACTIVE", now, new[]
        {
            (I("Brown Sugar Syrup").IngredientId, 35m),
            (I("Milk Powder").IngredientId, 30m),
            (I("Water").IngredientId, 250m),
            (I("Tapioca Pearls (Dry)").IngredientId, 40m), // simplified; real flow uses cooked pearls semi-finished
            (I("Ice Cubes").IngredientId, 180m),
            (I("Cup 500ml").IngredientId, 1m),
            (I("Lid 500ml").IngredientId, 1m),
            (I("Straw").IngredientId, 1m),
        });

        // Taro Milk Tea 500ml
        EnsureRecipe(db, P("FT-TARO-500").ProductId, 1, "ACTIVE",
            "Taro Milk Tea (500ml):\n1) Mix taro powder with hot water.\n2) Add milk base.\n3) Sweeten if needed.\n4) Add ice and shake.",
            now);
        EnsureBom(db, P("FT-TARO-500").ProductId, 1, "ACTIVE", now, new[]
        {
            (I("Taro Powder").IngredientId, 35m),
            (I("Milk Powder").IngredientId, 25m),
            (I("Water").IngredientId, 280m),
            (I("Sugar Syrup").IngredientId, 20m),
            (I("Ice Cubes").IngredientId, 180m),
            (I("Cup 500ml").IngredientId, 1m),
            (I("Lid 500ml").IngredientId, 1m),
            (I("Straw").IngredientId, 1m),
        });

        db.SaveChanges();
    }

    private static void EnsureRecipe(AppDbContext db, int productId, int version, string status, string instructions, DateTime now)
    {
        var existing = db.Recipes.FirstOrDefault(x => x.ProductId == productId && x.Version == version);
        if (existing != null)
        {
            if (!string.Equals(existing.Status, status, StringComparison.OrdinalIgnoreCase))
                existing.Status = status;

            // DB không có UpdatedAt/Instructions -> bỏ
            return;
        }

        db.Recipes.Add(new Recipe
        {
            ProductId = productId,
            Version = version,
            Status = status,
            CreatedAt = now
            // UpdatedAt/Instructions: bỏ
        });
    }
    private static void EnsureBom(
        AppDbContext db,
        int productId,
        int version,
        string status,
        DateTime now,
        (int ingredientId, decimal qty)[] items,
        bool allowFallback = false,
        (int ingredientId, decimal qty)[]? fallbackItems = null)
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
            db.SaveChanges(); // need BomId
        }
        else
        {
            var changed = false;
            if (!string.Equals(bom.Status, status, StringComparison.OrdinalIgnoreCase)) { bom.Status = status; changed = true; }
            if (changed) bom.UpdatedAt = now;
        }

        // If items includes a weird placeholder, use fallback
        var finalItems = items;

        if (allowFallback)
        {
            // guard: if any ingredientId is 0 (should never happen) use fallback
            if (items.Any(x => x.ingredientId <= 0) && fallbackItems != null)
                finalItems = fallbackItems;
        }

        // Idempotent upsert: ensure each (ingredientId) exists; do not delete user-added items
        var existingMap = bom.Items.ToDictionary(x => x.IngredientId, x => x);

        foreach (var (ingredientId, qty) in finalItems)
        {
            if (existingMap.TryGetValue(ingredientId, out var bi))
            {
                if (bi.Quantity != qty) bi.Quantity = qty;
            }
            else
            {
                db.BomItems.Add(new BomItem
                {
                    BomId = bom.BomId,
                    IngredientId = ingredientId,
                    Quantity = qty
                });
            }
        }
    }
}