using ECommerceRbac.Models;

namespace ECommerceRbac.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }

    public ICollection<Session> Sessions { get; set; } = new List<Session>();

    // Add this line to fix the CS1061 error
    public ICollection<Role> Roles { get; set; } = new List<Role>();
}