
using ECommerceRbac.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerceRbac.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Models.Endpoint> Endpoints => Set<Models.Endpoint>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // Use Code as the Primary Key for Permissions
        modelBuilder.Entity<Permission>().HasKey(p => p.Code);

        // Many-to-Many setup (EF Core creates the join tables automatically)
        modelBuilder.Entity<Role>().HasMany(r => r.Permissions).WithMany();
        modelBuilder.Entity<Models.Endpoint>().HasMany(e => e.Permissions).WithMany();
        modelBuilder.Entity<User>().HasMany(u => u.Roles).WithMany(r => r.Users);
        modelBuilder.Entity<Order>().HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId);
    }
}