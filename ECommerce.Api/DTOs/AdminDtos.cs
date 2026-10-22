namespace ECommerceRbac.DTOs;

public class CreateRoleDto
{
    public required string Name { get; set; }
}

public class CreatePermissionDto
{
    public required string Code { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class AssignPermissionDto
{
    public required string PermissionCode { get; set; }
}