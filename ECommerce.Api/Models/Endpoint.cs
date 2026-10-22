namespace ECommerceRbac.Models;

public class Endpoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Method { get; set; }
    public required string Route { get; set; }
    public required string AuthMode { get; set; }
    public string MatchMode { get; set; } = "ANY";
    public bool IsSystem { get; set; }

    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
}