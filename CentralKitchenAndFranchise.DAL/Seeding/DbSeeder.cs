using CentralKitchenAndFranchise.DAL.Entities;
using CentralKitchenAndFranchise.DTO.Constants;
using Microsoft.EntityFrameworkCore;

namespace CentralKitchenAndFranchise.DAL.Seeding;

public static class DbSeeder
{
    // Bạn đổi password mặc định ở đây (hoặc đọc từ ENV tuỳ bạn)
    private const string DefaultAdminUsername = "admin";
    private const string DefaultAdminEmail = "admin@gmail.com";
    private const string DefaultAdminPassword = "123456";

    public static void Seed(AppDbContext db)
    {
        SeedRoles(db);
        SeedAdmin(db);
        SeedSystemSettings(db);
    }

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

    private static void SeedAdmin(AppDbContext db)
    {
        var adminRoleId = db.Roles
            .Where(r => r.Name == RoleNames.Admin)
            .Select(r => r.RoleId)
            .FirstOrDefault();

        if (adminRoleId == 0)
            throw new InvalidOperationException("Admin role not found. Seeding roles failed?");

        var keyUsername = DefaultAdminUsername.ToLower();
        var keyEmail = DefaultAdminEmail.ToLower();

        var admin = db.Users.FirstOrDefault(u =>
            u.Username.ToLower() == keyUsername || u.Email.ToLower() == keyEmail);

        var now = DateTime.UtcNow;

        if (admin is null)
        {
            db.Users.Add(new User
            {
                Username = DefaultAdminUsername,
                Email = DefaultAdminEmail,
                RoleId = adminRoleId,
                Status = "ACTIVE",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultAdminPassword),
                CreatedAt = now,
                UpdatedAt = now
            });

            db.SaveChanges();
            return;
        }

        // Nếu đã có admin: đảm bảo role/status/have password
        var changed = false;

        if (admin.RoleId != adminRoleId)
        {
            admin.RoleId = adminRoleId;
            changed = true;
        }

        if (!string.Equals(admin.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            admin.Status = "ACTIVE";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(admin.PasswordHash))
        {
            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultAdminPassword);
            changed = true;
        }

        if (changed)
        {
            admin.UpdatedAt = now;
            db.SaveChanges();
        }
    }

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

        // existing
        Ensure(SettingKeys.NearExpiryDays, "7", "Near-expiry definition window in days");

        //ordering
        Ensure(SettingKeys.FutureOrderLimitDays, "7", "Max future order creation window in days (FR-018)");
        Ensure(SettingKeys.OrderEditWindowMinutes, "30", "Allowed edit window after submit in minutes (FR-038)");

        db.SaveChanges();
    }

}
