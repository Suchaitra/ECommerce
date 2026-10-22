using ECommerceRbac.Models;

namespace ECommerceRbac.Models;

public class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public bool IsSystem { get; set; } // System roles (like Super Admin) cannot be deleted

    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
    public ICollection<User> Users { get; set; } = new List<User>();
}