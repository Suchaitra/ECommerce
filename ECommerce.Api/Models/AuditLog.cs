namespace ECommerceRbac.Models;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public required string Action { get; set; } // "Created", "Updated", "Deleted"
    public required string Resource { get; set; } // "Role", "Permission", "Endpoint"
    public required string Details { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}