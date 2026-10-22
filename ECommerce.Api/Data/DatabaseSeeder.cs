
using ECommerceRbac.Data;
using ECommerceRbac.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ECommerceRbac.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        // 1. Seed System Permissions
        var systemPermissions = new[]
        {
            new Permission { Code = "endpoint_manage", Description = "Manage endpoints" },
            new Permission { Code = "permission_manage", Description = "Manage permissions" },
            new Permission { Code = "role_manage", Description = "Manage roles" },
            new Permission { Code = "user_manage", Description = "Manage users" },
            new Permission { Code = "audit_read", Description = "View audit logs" }
        };

        foreach (var p in systemPermissions)
        {
            if (!await db.Permissions.AnyAsync(x => x.Code == p.Code))
                db.Permissions.Add(p);
        }

        // 2. Seed System Roles
        var superAdmin = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Name == "Super admin");
        if (superAdmin == null)
        {
            superAdmin = new Role { Name = "Super admin", IsSystem = true };
            foreach (var p in systemPermissions) superAdmin.Permissions.Add(p);
            db.Roles.Add(superAdmin);
        }

        if (!await db.Roles.AnyAsync(r => r.Name == "Customer"))
        {
            db.Roles.Add(new Role { Name = "Customer", IsSystem = true });
        }

        // 3. Seed First Admin User
        var adminEmail = "admin@system.local";
        var admin = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == adminEmail);
        if (admin == null)
        {
            admin = new User { Email = adminEmail, PasswordHash = "" };
            var hasher = new PasswordHasher<User>();
            admin.PasswordHash = hasher.HashPassword(admin, "Admin123!");
            admin.Roles.Add(superAdmin);
            db.Users.Add(admin);
        }

        await db.SaveChangesAsync();
    }
}