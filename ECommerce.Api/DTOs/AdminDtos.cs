namespace ECommerceRbac.DTOs;

public class CreateRoleDto
{
    public required string Name { get; set; }
}



public class AssignPermissionDto
{
    public required string PermissionCode { get; set; }
}