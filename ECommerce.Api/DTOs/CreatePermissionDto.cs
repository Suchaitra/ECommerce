public class CreatePermissionDto
{
    public required string Code { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? Category { get; set; }
}