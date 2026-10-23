using System.Text.RegularExpressions;
using ECommerceRbac.Attributes;
using ECommerceRbac.Data;
using ECommerceRbac.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceRbac.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
// REMOVED class-level [RbacEndpoint] to allow granular scoping
public class AdminController : ControllerBase
{
    private static readonly string[] Categories = { "Read", "Create", "Update", "Delete", "Other" };
    private static readonly string[] MatchModes = { "All", "Any" };
    private static readonly Regex CodePattern = new("^[a-z][a-z0-9_]{1,99}$", RegexOptions.Compiled);

    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("endpoints")]
    [RbacEndpoint("Permission", isSystem: true)]
    public async Task<IActionResult> GetEndpoints(
        [FromQuery] string? category,
        [FromQuery] string? resource,
        [FromQuery] bool? unmapped)
    {
        string? categoryFilter = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            categoryFilter = NormalizeCategory(category);
            if (categoryFilter == null)
                return BadRequest(new { Message = $"Category must be one of: {string.Join(", ", Categories)}." });
        }

        var all = await _db.Endpoints
            .Include(e => e.Permissions)
            .OrderBy(e => e.Route)
            .ToListAsync();

        var rows = all.Select(e => new
        {
            e.Id,
            e.Route,
            e.AuthMode,
            e.IsSystem,
            e.MatchMode,
            IsUnmapped = e.AuthMode == "Permission" && !e.IsSystem && !e.Permissions.Any(),
            Categories = e.Permissions.Select(p => CategoryOf(p)).Distinct().OrderBy(c => c).ToList(),
            Resources = e.Permissions
                .Select(p => p.Resource)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct()
                .OrderBy(r => r)
                .ToList(),
            Permissions = e.Permissions
                .OrderBy(p => p.Code)
                .Select(p => new { p.Code, p.Resource, Category = CategoryOf(p) })
                .ToList()
        }).ToList();

        var summary = new
        {
            Total = rows.Count,
            Unmapped = rows.Count(r => r.IsUnmapped),
            System = rows.Count(r => r.IsSystem),
            ByCategory = Categories.ToDictionary(c => c, c => rows.Count(r => r.Categories.Contains(c))),
            Grid = rows
                .SelectMany(r => r.Permissions.Select(p => new { p.Resource, p.Category, r.Id }))
                .Where(x => !string.IsNullOrWhiteSpace(x.Resource))
                .Distinct()
                .GroupBy(x => x.Resource)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Resource = g.Key,
                    Read = g.Count(x => x.Category == "Read"),
                    Create = g.Count(x => x.Category == "Create"),
                    Update = g.Count(x => x.Category == "Update"),
                    Delete = g.Count(x => x.Category == "Delete"),
                    Other = g.Count(x => x.Category == "Other")
                })
                .ToList()
        };

        var filtered = rows.Where(r =>
            (categoryFilter == null || r.Categories.Contains(categoryFilter)) &&
            (string.IsNullOrWhiteSpace(resource) || r.Resources.Contains(resource.Trim(), StringComparer.OrdinalIgnoreCase)) &&
            (unmapped == null || r.IsUnmapped == unmapped.Value))
            .ToList();

        return Ok(new { Summary = summary, Endpoints = filtered });
    }

    [HttpGet("permissions")]
    [RbacEndpoint("Permission", isSystem: true)]
    public async Task<IActionResult> GetPermissions()
    {
        var permissions = await _db.Permissions
            .OrderBy(p => p.Resource)
            .ThenBy(p => p.Code)
            .ToListAsync();

        var endpointUse = (await _db.Endpoints.SelectMany(e => e.Permissions.Select(p => p.Code)).ToListAsync())
            .GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        var roleUse = (await _db.Roles.SelectMany(r => r.Permissions.Select(p => p.Code)).ToListAsync())
            .GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());

        return Ok(permissions.Select(p => new
        {
            p.Code,
            p.Description,
            p.Resource,
            Category = CategoryOf(p),
            UsedByEndpoints = endpointUse.GetValueOrDefault(p.Code),
            UsedByRoles = roleUse.GetValueOrDefault(p.Code)
        }));
    }

    [HttpPost("permissions")]
    [RbacEndpoint("Permission", isSystem: true)]
    public async Task<IActionResult> CreatePermission([FromBody] CreatePermissionDto dto)
    {
        var code = dto.Code.Trim().ToLowerInvariant();
        if (!CodePattern.IsMatch(code))
            return BadRequest(new { Message = "Code must use lowercase letters, digits and underscores, starting with a letter (for example product_create)." });

        var category = string.IsNullOrWhiteSpace(dto.Category) ? "Other" : NormalizeCategory(dto.Category);
        if (category == null)
            return BadRequest(new { Message = $"Category must be one of: {string.Join(", ", Categories)}." });

        if (await _db.Permissions.AnyAsync(p => p.Code == code))
            return BadRequest(new { Message = "Permission code already exists." });

        var permission = new Models.Permission
        {
            Code = code,
            Description = dto.Description,
            Resource = dto.Resource.Trim().ToLowerInvariant(),
            Category = category
        };
        _db.Permissions.Add(permission);
        await _db.SaveChangesAsync();

        return Ok(new { permission.Code, permission.Description, permission.Resource, permission.Category });
    }

    [HttpGet("roles")]
    [RbacEndpoint("Permission", isSystem: true)]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _db.Roles
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.IsSystem,
                Permissions = r.Permissions.Select(p => p.Code)
            })
            .ToListAsync();

        return Ok(roles);
    }

    [HttpPost("roles")]
    [RbacEndpoint("Permission", isSystem: true)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleDto dto)
    {
        if (await _db.Roles.AnyAsync(r => r.Name == dto.Name))
            return BadRequest(new { Message = "Role name already exists." });

        var role = new Models.Role { Name = dto.Name, IsSystem = false };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        return Ok(new { role.Id, role.Name, role.IsSystem });
    }

    [HttpPost("roles/{id}/permissions")]
    [RbacEndpoint("Permission", isSystem: true)]
    public async Task<IActionResult> AssignPermissionToRole(Guid id, [FromBody] AssignPermissionDto dto)
    {
        var role = await _db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound(new { Message = "Role not found." });

        var permission = await _db.Permissions.FirstOrDefaultAsync(p => p.Code == dto.PermissionCode);
        if (permission == null) return NotFound(new { Message = "Permission not found." });

        if (!role.Permissions.Any(p => p.Code == permission.Code))
        {
            role.Permissions.Add(permission);
            await _db.SaveChangesAsync();
        }

        return Ok(new { Message = "Permission assigned successfully." });
    }

    [HttpDelete("roles/{id}/permissions/{code}")]
    [RbacEndpoint("Permission", isSystem: true)]
    public async Task<IActionResult> RevokePermissionFromRole(Guid id, string code)
    {
        var role = await _db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound(new { Message = "Role not found." });

        var permission = role.Permissions.FirstOrDefault(p => p.Code == code);
        if (permission != null)
        {
            var protectsSystemEndpoint = await _db.Endpoints
                .AnyAsync(e => e.IsSystem && e.Permissions.Any(p => p.Code == code));
            if (protectsSystemEndpoint)
            {
                var heldElsewhere = await _db.Roles
                    .AnyAsync(r => r.Id != id && r.Permissions.Any(p => p.Code == code));
                if (!heldElsewhere)
                    return Conflict(new { Message = "This is the last role holding a permission that protects the admin API, so it can't be revoked." });
            }

            role.Permissions.Remove(permission);
            await _db.SaveChangesAsync();
        }

        return Ok(new { Message = "Permission revoked successfully." });
    }

    // FIXED: Route changed to plural "permissions" and duplicate attribute removed
    [HttpGet("endpoints/{id}/permissions")]
    [RbacEndpoint("Permission", isSystem: true)]
    public async Task<IActionResult> GetEndpointPermissions(Guid id)
    {
        var endpoint = await _db.Endpoints
            .Include(e => e.Permissions)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (endpoint == null)
        {
            return NotFound(new { Message = "Endpoint not found." });
        }

        return Ok(new
        {
            endpoint.MatchMode,
            Permissions = endpoint.Permissions
                .OrderBy(p => p.Code)
                .Select(p => new { p.Code, p.Description, p.Resource, Category = CategoryOf(p) })
        });
    }

    [HttpPut("endpoints/{id}/permissions")]
    [RbacEndpoint("Permission", isSystem: true)]
    public async Task<IActionResult> SetEndpointPermissions(Guid id, [FromBody] SetEndpointPermissionsDto dto)
    {
        var endpoint = await _db.Endpoints.Include(e => e.Permissions).FirstOrDefaultAsync(e => e.Id == id);
        if (endpoint == null) return NotFound(new { Message = "Endpoint not found." });

        var blocked = EndpointMappingBlock(endpoint.IsSystem, endpoint.AuthMode);
        if (blocked != null) return blocked;

        var matchMode = endpoint.MatchMode;
        if (!string.IsNullOrWhiteSpace(dto.MatchMode))
        {
            var requested = MatchModes.FirstOrDefault(m => m.Equals(dto.MatchMode.Trim(), StringComparison.OrdinalIgnoreCase));
            if (requested == null)
                return BadRequest(new { Message = $"MatchMode must be one of: {string.Join(", ", MatchModes)}." });
            matchMode = requested;
        }

        var codes = dto.PermissionCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        var permissions = await _db.Permissions.Where(p => codes.Contains(p.Code)).ToListAsync();
        var missing = codes.Except(permissions.Select(p => p.Code)).ToList();
        if (missing.Count > 0)
            return NotFound(new { Message = "Some permissions don't exist.", Missing = missing });

        endpoint.Permissions.Clear();
        foreach (var permission in permissions)
            endpoint.Permissions.Add(permission);
        endpoint.MatchMode = matchMode;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            endpoint.Id,
            endpoint.Route,
            endpoint.MatchMode,
            Permissions = permissions.Select(p => p.Code).OrderBy(c => c)
        });
    }

    [HttpPost("endpoints/{id}/permissions")]
    [RbacEndpoint("Permission", isSystem: true)]
    public async Task<IActionResult> AssignPermissionToEndpoint(Guid id, [FromBody] AssignPermissionDto dto)
    {
        var endpoint = await _db.Endpoints.Include(e => e.Permissions).FirstOrDefaultAsync(e => e.Id == id);
        if (endpoint == null) return NotFound(new { Message = "Endpoint not found." });

        var blocked = EndpointMappingBlock(endpoint.IsSystem, endpoint.AuthMode);
        if (blocked != null) return blocked;

        var permission = await _db.Permissions.FirstOrDefaultAsync(p => p.Code == dto.PermissionCode);
        if (permission == null) return NotFound(new { Message = "Permission not found." });

        if (!endpoint.Permissions.Any(p => p.Code == permission.Code))
        {
            endpoint.Permissions.Add(permission);
            await _db.SaveChangesAsync();
        }

        return Ok(new { Message = "Permission assigned to endpoint." });
    }

    [HttpDelete("endpoints/{id}/permissions/{code}")]
    [RbacEndpoint("Permission", isSystem: true)]
    public async Task<IActionResult> RevokePermissionFromEndpoint(Guid id, string code)
    {
        var endpoint = await _db.Endpoints.Include(e => e.Permissions).FirstOrDefaultAsync(e => e.Id == id);
        if (endpoint == null) return NotFound(new { Message = "Endpoint not found." });

        var blocked = EndpointMappingBlock(endpoint.IsSystem, endpoint.AuthMode);
        if (blocked != null) return blocked;

        var permission = endpoint.Permissions.FirstOrDefault(p => p.Code == code);
        if (permission != null)
        {
            endpoint.Permissions.Remove(permission);
            await _db.SaveChangesAsync();
        }

        return Ok(new { Message = "Permission revoked from endpoint." });
    }

    [HttpGet("audit")]
    [RbacEndpoint("Permission", isSystem: true)] // Now strictly scopes this method separately
    public async Task<IActionResult> GetAuditLogs()
    {
        var logs = await _db.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .Take(100)
            .ToListAsync();

        return Ok(logs);
    }

    private IActionResult? EndpointMappingBlock(bool isSystem, string authMode)
    {
        if (isSystem)
            return StatusCode(StatusCodes.Status403Forbidden, new { Message = "System endpoints can't be remapped." });

        if (authMode != "Permission")
            return BadRequest(new { Message = "Only endpoints with AuthMode 'Permission' can have permissions mapped." });

        return null;
    }

    private static string CategoryOf(Models.Permission permission) =>
        string.IsNullOrWhiteSpace(permission.Category) ? "Other" : permission.Category;

    private static string? NormalizeCategory(string? value) =>
        Categories.FirstOrDefault(c => c.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase));
}