using System.Text.Json;
using CentralKitchenAndFranchise.BLL.Services.Interfaces;
using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DAL.Seeding;
using CentralKitchenAndFranchise.DTO.Constants;
using CentralKitchenAndFranchise.DTO.Responses;
using CentralKitchenAndFranchise.DTO.Responses.Seed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.API.Controllers;

[ApiController]
[Route("api/admin/seed")]
[Authorize(Roles = RoleNames.Admin)]
[AllowAnonymous]
public class AdminSeedController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IWebHostEnvironment _env;

    public AdminSeedController(AppDbContext db, ICurrentUserService current, IWebHostEnvironment env)
    {
        _db = db;
        _current = current;
        _env = env;
    }

    /// <summary>
    /// DEV-ONLY: Drop ALL data by truncating every table in schema public
    /// (except __EFMigrationsHistory), then re-run DbSeeder.Seed(db) to restore
    /// base defaults (roles, default accounts, system settings, milk-tea BOM/Recipe).
    /// Admin-only, Development-only.
    /// </summary>
    [HttpPost("reset-all")]
    public async Task<ActionResult<ApiResponse<SeedResetResponse>>> ResetAll(CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            throw new UnauthorizedAccessException("Seed API is only available in Development environment.");

        // Defensive: ensure caller still exists (avoid weird states during reset)
        var caller = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == _current.UserId, ct);
        if (caller is null)
            throw new UnauthorizedAccessException("Current user not found.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // List tables to be truncated (for reporting)
        var tables = await _db.Database
            .SqlQueryRaw<string>(
                "SELECT tablename FROM pg_tables WHERE schemaname='public' AND tablename <> '__EFMigrationsHistory' ORDER BY tablename")
            .ToListAsync(ct);

        const string truncateAllSql = @"
DO $$
DECLARE r RECORD;
BEGIN
  FOR r IN (
    SELECT tablename
    FROM pg_tables
    WHERE schemaname='public'
      AND tablename <> '__EFMigrationsHistory'
  ) LOOP
    EXECUTE 'TRUNCATE TABLE ' || quote_ident(r.tablename) || ' RESTART IDENTITY CASCADE';
  END LOOP;
END $$;";

        await _db.Database.ExecuteSqlRawAsync(truncateAllSql, ct);

        // Re-seed baseline so you don't lock yourself out after truncating users/roles
        DbSeeder.Seed(_db);

        await tx.CommitAsync(ct);

        var resp = new SeedResetResponse
        {
            ResetDone = true,
            ReseededBaseData = true,
            TablesTruncated = tables.Count,
            TruncatedTables = tables,
            DefaultAccounts = new List<SeedAccountInfo>
            {
                new() { Username = "admin",    Email = "admin@gmail.com",    Role = RoleNames.Admin },
                new() { Username = "manager",  Email = "manager@gmail.com",  Role = RoleNames.Manager },
                new() { Username = "supply",   Email = "supply@gmail.com",   Role = RoleNames.SupplyCoordinator },
                new() { Username = "kitchen",  Email = "kitchen@gmail.com",  Role = RoleNames.KitchenStaff },
                new() { Username = "store.q1", Email = "store.q1@gmail.com", Role = RoleNames.StoreStaff },
                new() { Username = "store.q7", Email = "store.q7@gmail.com", Role = RoleNames.StoreStaff },
            }
        };

        return Ok(ApiResponse<SeedResetResponse>.Ok(resp, "Reset all data + reseed base defaults completed."));
    }

    /// <summary>
    /// Seed VALID sample data for local testing (Assignment 1).
    /// Admin-only, Development-only.
    /// Idempotent: safe to call multiple times.
    /// </summary>
    [HttpPost("sample-data")]
    public async Task<ActionResult<ApiResponse<SeedSampleDataResponse>>> SeedSampleData(CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            throw new UnauthorizedAccessException("Seed API is only available in Development environment.");

        var sentinelFranchiseName = "Franchise Store A";
        var sentinelSku = "SKU-MILKTEA-01";

        var alreadySeeded =
            await _db.Franchises.AsNoTracking().AnyAsync(x => x.Name == sentinelFranchiseName, ct)
            && await _db.Products.AsNoTracking().AnyAsync(x => x.Sku == sentinelSku, ct);

        var resp = new SeedSampleDataResponse { AlreadySeeded = alreadySeeded };
        if (alreadySeeded)
            return Ok(ApiResponse<SeedSampleDataResponse>.Ok(resp, "Sample data already seeded."));

        // Ensure required roles exist (DbSeeder should have created them)
        var adminRoleId = await _db.Roles.Where(r => r.Name == RoleNames.Admin).Select(r => r.RoleId).FirstOrDefaultAsync(ct);
        var managerRoleId = await _db.Roles.Where(r => r.Name == RoleNames.Manager).Select(r => r.RoleId).FirstOrDefaultAsync(ct);

        // NOTE: change RoleNames.StoreStaff if your codebase uses different constant
        var storeStaffRoleId = await _db.Roles.Where(r => r.Name == RoleNames.StoreStaff).Select(r => r.RoleId).FirstOrDefaultAsync(ct);

        if (adminRoleId == 0 || managerRoleId == 0 || storeStaffRoleId == 0)
            throw new InvalidOperationException("Required roles not found. Ensure DbSeeder.Seed(db) ran.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var now = DateTime.UtcNow;

        // 1) Franchises
        var (franchiseAId, createdA) = await EnsureFranchiseAsync(
            name: "Franchise Store A",
            type: "STORE",
            address: "Sample Address A",
            location: "Sample Location A",
            latitude: 10.0,
            longitude: 106.0,
            ct: ct);

        if (createdA) resp.FranchisesCreated++;
        resp.FranchiseIds.Add(franchiseAId);

        var (franchiseBId, createdB) = await EnsureFranchiseAsync(
            name: "Franchise Store B",
            type: "STORE",
            address: "Sample Address B",
            location: "Sample Location B",
            latitude: 10.1,
            longitude: 106.1,
            ct: ct);

        if (createdB) resp.FranchisesCreated++;
        resp.FranchiseIds.Add(franchiseBId);

        // 2) Users
        var (manager, createdManager) = await EnsureUserAsync(
            username: "manager1",
            email: "manager1@gmail.com",
            roleId: managerRoleId,
            passwordPlain: "123456",
            ct: ct);

        if (createdManager) resp.UsersCreated++;
        resp.UserIds.Add(manager.UserId);

        var (storeA, createdStoreA) = await EnsureUserAsync(
            username: "storea1",
            email: "storea1@gmail.com",
            roleId: storeStaffRoleId,
            passwordPlain: "123456",
            ct: ct);

        if (createdStoreA) resp.UsersCreated++;
        resp.UserIds.Add(storeA.UserId);

        var (storeB, createdStoreB) = await EnsureUserAsync(
            username: "storeb1",
            email: "storeb1@gmail.com",
            roleId: storeStaffRoleId,
            passwordPlain: "123456",
            ct: ct);

        if (createdStoreB) resp.UsersCreated++;
        resp.UserIds.Add(storeB.UserId);

        // Assign store staff to OU (IMPORTANT: unique index on UserId => upsert by UserId)
        await EnsureUserFranchiseAsync(storeA.UserId, franchiseAId, ct);
        await EnsureUserFranchiseAsync(storeB.UserId, franchiseBId, ct);

        // 3) Suppliers
        var (supplierAlphaId, createdSup1) = await EnsureSupplierAsync(
            name: "Supplier Alpha",
            contactInfo: "alpha@supplier.com",
            ct: ct);

        if (createdSup1) resp.SuppliersCreated++;
        resp.SupplierIds.Add(supplierAlphaId);

        var (supplierBetaId, createdSup2) = await EnsureSupplierAsync(
            name: "Supplier Beta",
            contactInfo: "beta@supplier.com",
            ct: ct);

        if (createdSup2) resp.SuppliersCreated++;
        resp.SupplierIds.Add(supplierBetaId);

        // 4) Ingredients
        var ingredientsToEnsure = new[]
        {
            new IngredientSeed("Black Tea", "g",   0.02m,   5000,  0.05m),
            new IngredientSeed("Milk",     "ml",  0.01m,  20000,  0.05m),
            new IngredientSeed("Sugar",    "g",   0.005m, 10000,  0.03m),
            new IngredientSeed("Tapioca Pearls","g",0.03m, 8000,  0.08m),
            new IngredientSeed("Ice",      "g",   0.0002m,30000,  0.10m),
            new IngredientSeed("Cocoa Powder","g",0.04m,   3000,  0.06m),
        };

        foreach (var i in ingredientsToEnsure)
        {
            var (id, created) = await EnsureIngredientAsync(i, ct);
            if (created) resp.IngredientsCreated++;
            resp.IngredientIds.Add(id);
        }

        // 5) Products
        var productsToEnsure = new[]
        {
            new ProductSeed("Milk Tea",       "SKU-MILKTEA-01", "cup", "FINISHED"),
            new ProductSeed("Pearl Milk Tea", "SKU-PEARL-01",   "cup", "FINISHED"),
            new ProductSeed("Chocolate Milk", "SKU-CHOCO-01",   "cup", "FINISHED"),
            new ProductSeed("Cooked Pearls",  "SKU-PEARLS-SS",  "g",   "SEMI_FINISHED"),
            new ProductSeed("Sugar Syrup",    "SKU-SYRUP-SS",   "ml",  "SEMI_FINISHED"),
            new ProductSeed("Tea Base",       "SKU-TEA-SS",     "ml",  "SEMI_FINISHED"),
        };

        foreach (var p in productsToEnsure)
        {
            var (id, created) = await EnsureProductAsync(p, ct);
            if (created) resp.ProductsCreated++;
            resp.ProductIds.Add(id);
        }

        // 6) Store Catalog mapping
        var allActiveProducts = await _db.Products.AsNoTracking().Where(x => x.Status == "ACTIVE").ToListAsync(ct);

        foreach (var product in allActiveProducts)
        {
            var (createdMapA, _) = await EnsureStoreCatalogAsync(franchiseAId, product, basePrice: 25000m, ct);
            if (createdMapA) resp.StoreCatalogItemsCreated++;

            var (createdMapB, _) = await EnsureStoreCatalogAsync(franchiseBId, product, basePrice: 27000m, ct);
            if (createdMapB) resp.StoreCatalogItemsCreated++;
        }

        // 7) One audit log entry for traceability
        await _db.AuditLogs.AddAsync(new AuditLog
        {
            UserId = _current.UserId,
            Action = "SEED_SAMPLE_DATA",
            EntityName = "Seed",
            EntityId = null,
            NewDataJson = JsonSerializer.Serialize(new
            {
                resp.FranchisesCreated,
                resp.UsersCreated,
                resp.SuppliersCreated,
                resp.IngredientsCreated,
                resp.ProductsCreated,
                resp.StoreCatalogItemsCreated,
                FranchiseIds = resp.FranchiseIds
            }),
            Reason = "Seed valid sample data for testing (DEV-only).",
            CreatedAt = DateTime.UtcNow
        }, ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(ApiResponse<SeedSampleDataResponse>.Ok(resp, "Seed sample data completed."));
    }

    // ==========================================================
    // Helpers
    // ==========================================================
    private async Task<(int franchiseId, bool created)> EnsureFranchiseAsync(
        string name,
        string type,
        string address,
        string location,
        double latitude,
        double longitude,
        CancellationToken ct)
    {
        var existing = await _db.Franchises.FirstOrDefaultAsync(x => x.Name == name, ct);
        if (existing is not null)
        {
            // normalize active for testing convenience
            var changed = false;

            if (!string.Equals(existing.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                existing.Status = "ACTIVE";
                changed = true;
            }

            if (changed)
            {
                existing.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            return (existing.FranchiseId, false);
        }

        var now = DateTime.UtcNow;
        var entity = new Franchise
        {
            Name = name,
            Type = type,
            Status = "ACTIVE",
            Address = address,
            Location = location,
            Latitude = latitude,
            Longitude = longitude,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _db.Franchises.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        return (entity.FranchiseId, true);
    }

    private async Task<(User user, bool created)> EnsureUserAsync(
        string username,
        string email,
        int roleId,
        string passwordPlain,
        CancellationToken ct)
    {
        var keyU = username.Trim().ToLowerInvariant();
        var keyE = email.Trim().ToLowerInvariant();

        var existing = await _db.Users.FirstOrDefaultAsync(
            u => u.Username.ToLower() == keyU || u.Email.ToLower() == keyE,
            ct);

        if (existing is not null)
        {
            var changed = false;

            if (existing.RoleId != roleId) { existing.RoleId = roleId; changed = true; }
            if (!string.Equals(existing.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)) { existing.Status = "ACTIVE"; changed = true; }
            if (string.IsNullOrWhiteSpace(existing.PasswordHash))
            {
                existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordPlain);
                changed = true;
            }

            if (changed)
            {
                existing.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            return (existing, false);
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Username = username.Trim(),
            Email = email.Trim(),
            RoleId = roleId,
            Status = "ACTIVE",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordPlain),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _db.Users.AddAsync(user, ct);
        await _db.SaveChangesAsync(ct);

        return (user, true);
    }

    private async Task EnsureUserFranchiseAsync(int userId, int franchiseId, CancellationToken ct)
    {
        // IMPORTANT: your DB has UNIQUE index on UserId (1 franchise per user)
        var existing = await _db.UserFranchises.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (existing is null)
        {
            await _db.UserFranchises.AddAsync(new UserFranchise
            {
                UserId = userId,
                FranchiseId = franchiseId,
                AssignedAt = DateTime.UtcNow
            }, ct);

            await _db.SaveChangesAsync(ct);
            return;
        }

        if (existing.FranchiseId != franchiseId)
        {
            existing.FranchiseId = franchiseId;
            existing.AssignedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<(int supplierId, bool created)> EnsureSupplierAsync(string name, string contactInfo, CancellationToken ct)
    {
        var existing = await _db.Suppliers.FirstOrDefaultAsync(x => x.Name == name, ct);
        if (existing is not null)
        {
            var changed = false;
            if (!string.Equals(existing.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                existing.Status = "ACTIVE";
                changed = true;
            }

            if (changed)
                await _db.SaveChangesAsync(ct);

            return (existing.SupplierId, false);
        }

        var s = new Supplier
        {
            Name = name,
            ContactInfo = contactInfo,
            Status = "ACTIVE"
        };

        await _db.Suppliers.AddAsync(s, ct);
        await _db.SaveChangesAsync(ct);

        return (s.SupplierId, true);
    }

    private async Task<(int ingredientId, bool created)> EnsureIngredientAsync(IngredientSeed seed, CancellationToken ct)
    {
        var existing = await _db.Ingredients.FirstOrDefaultAsync(x => x.Name == seed.Name, ct);
        if (existing is not null)
        {
            var changed = false;

            if (!string.Equals(existing.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                existing.Status = "ACTIVE";
                changed = true;
            }

            // keep your demo consistent
            if (existing.Unit != seed.Unit) { existing.Unit = seed.Unit; changed = true; }
            if (existing.Price != seed.Price) { existing.Price = seed.Price; changed = true; }
            if (existing.SafetyStock != seed.SafetyStock) { existing.SafetyStock = seed.SafetyStock; changed = true; }
            if (existing.WasteThreshold != seed.WasteThreshold) { existing.WasteThreshold = seed.WasteThreshold; changed = true; }

            if (changed)
            {
                existing.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            return (existing.IngredientId, false);
        }

        var now = DateTime.UtcNow;
        var entity = new Ingredient
        {
            Name = seed.Name,
            Unit = seed.Unit,
            Status = "ACTIVE",
            Price = seed.Price,
            SafetyStock = seed.SafetyStock,
            WasteThreshold = seed.WasteThreshold,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _db.Ingredients.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        return (entity.IngredientId, true);
    }

    private async Task<(int productId, bool created)> EnsureProductAsync(ProductSeed seed, CancellationToken ct)
    {
        var existing = await _db.Products.FirstOrDefaultAsync(x => x.Sku == seed.Sku, ct);
        if (existing is not null)
        {
            var changed = false;

            if (!string.Equals(existing.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                existing.Status = "ACTIVE";
                changed = true;
            }

            if (existing.Name != seed.Name) { existing.Name = seed.Name; changed = true; }
            if (existing.Unit != seed.Unit) { existing.Unit = seed.Unit; changed = true; }
            if (existing.ProductType != seed.ProductType) { existing.ProductType = seed.ProductType; changed = true; }

            if (changed)
            {
                // If Product has UpdatedAt in your schema, set it; if not, keep as-is.
                // existing.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            return (existing.ProductId, false);
        }

        var entity = new Product
        {
            Name = seed.Name,
            Sku = seed.Sku,
            Unit = seed.Unit,
            Status = "ACTIVE",
            ProductType = seed.ProductType
        };

        await _db.Products.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        return (entity.ProductId, true);
    }

    private async Task<(bool created, StoreCatalog entity)> EnsureStoreCatalogAsync(
        int franchiseId,
        Product product,
        decimal basePrice,
        CancellationToken ct)
    {
        var existing = await _db.StoreCatalogs.FirstOrDefaultAsync(x => x.FranchiseId == franchiseId && x.ProductId == product.ProductId, ct);
        if (existing is not null)
        {
            if (!string.Equals(existing.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                existing.Status = "ACTIVE";
                existing.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            return (false, existing);
        }

        var price = basePrice;
        if (string.Equals(product.ProductType, "SEMI_FINISHED", StringComparison.OrdinalIgnoreCase))
            price = basePrice * 0.4m;

        var now = DateTime.UtcNow;
        var entity = new StoreCatalog
        {
            FranchiseId = franchiseId,
            ProductId = product.ProductId,
            Price = price,
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now
        };

        await _db.StoreCatalogs.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        return (true, entity);
    }

    private sealed record IngredientSeed(string Name, string Unit, decimal Price, int SafetyStock, decimal WasteThreshold);
    private sealed record ProductSeed(string Name, string Sku, string Unit, string ProductType);
}