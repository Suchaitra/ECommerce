namespace ECommerceRbac.DTOs;

public class SetEndpointPermissionsDto
{
    public string? MatchMode { get; set; }
    public List<string> PermissionCodes { get; set; } = new();
}