namespace ECommerceRbac.Models;

public class Permission
{
    public required string Code { get; set; } // Primary Key (e.g., "order_create")
    public string Description { get; set; } = string.Empty;
    public string? Resource { get; set; }
    public string? Category { get; set; }
}