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
[RbacEndpoint("Permission", isSystem: true)]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("endpoints")]
    public async Task<IActionResult> GetEndpoints()
    {
        var endpoints = await _db.Endpoints
            .Include(e => e.Permissions)
            .OrderBy(e => e.Route)
            .ToListAsync();

        var summary = new
        {
            Total = endpoints.Count,
            Unmapped = endpoints.Count(e => e.AuthMode == "Permission" && !e.Permissions.Any() && !e.IsSystem),
            System = endpoints.Count(e => e.IsSystem)
        };

        return Ok(new { Summary = summary, Endpoints = endpoints });
    }

    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions()
    {
        var permissions = await _db.Permissions.ToListAsync();
        return Ok(permissions);
    }

    [HttpPost("permissions")]
    public async Task<IActionResult> CreatePermission([FromBody] CreatePermissionDto dto)
    {
        if (await _db.Permissions.AnyAsync(p => p.Code == dto.Code))
            return BadRequest(new { Message = "Permission code already exists." });

        var permission = new Models.Permission { Code = dto.Code, Description = dto.Description };
        _db.Permissions.Add(permission);
        await _db.SaveChangesAsync();

        return Ok(permission);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _db.Roles
            .Include(r => r.Permissions)
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
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleDto dto)
    {
        if (await _db.Roles.AnyAsync(r => r.Name == dto.Name))
            return BadRequest(new { Message = "Role name already exists." });

        var role = new Models.Role { Name = dto.Name, IsSystem = false };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        return Ok(role);
    }

    [HttpPost("roles/{id}/permissions")]
    public async Task<IActionResult> AssignPermissionToRole(Guid id, [FromBody] AssignPermissionDto dto)
    {
        var role = await _db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound(new { Message = "Role not found." });

        var permission = await _db.Permissions.FindAsync(dto.PermissionCode);
        if (permission == null) return NotFound(new { Message = "Permission not found." });

        if (!role.Permissions.Any(p => p.Code == permission.Code))
        {
            role.Permissions.Add(permission);
            await _db.SaveChangesAsync();
        }

        return Ok(new { Message = "Permission assigned successfully." });
    }

    [HttpDelete("roles/{id}/permissions/{code}")]
    public async Task<IActionResult> RevokePermissionFromRole(Guid id, string code)
    {
        var role = await _db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound(new { Message = "Role not found." });

        var permission = role.Permissions.FirstOrDefault(p => p.Code == code);
        if (permission != null)
        {
            role.Permissions.Remove(permission);
            await _db.SaveChangesAsync();
        }

        return Ok(new { Message = "Permission revoked successfully." });
    }

    [HttpPost("endpoints/{id}/permissions")]
    public async Task<IActionResult> AssignPermissionToEndpoint(Guid id, [FromBody] AssignPermissionDto dto)
    {
        var endpoint = await _db.Endpoints.Include(e => e.Permissions).FirstOrDefaultAsync(e => e.Id == id);
        if (endpoint == null) return NotFound(new { Message = "Endpoint not found." });

        var permission = await _db.Permissions.FindAsync(dto.PermissionCode);
        if (permission == null) return NotFound(new { Message = "Permission not found." });

        if (!endpoint.Permissions.Any(p => p.Code == permission.Code))
        {
            endpoint.Permissions.Add(permission);
            await _db.SaveChangesAsync();
        }

        return Ok(new { Message = "Permission assigned to endpoint." });
    }

    [HttpDelete("endpoints/{id}/permissions/{code}")]
    public async Task<IActionResult> RevokePermissionFromEndpoint(Guid id, string code)
    {
        var endpoint = await _db.Endpoints.Include(e => e.Permissions).FirstOrDefaultAsync(e => e.Id == id);
        if (endpoint == null) return NotFound(new { Message = "Endpoint not found." });

        var permission = endpoint.Permissions.FirstOrDefault(p => p.Code == code);
        if (permission != null)
        {
            endpoint.Permissions.Remove(permission);
            await _db.SaveChangesAsync();
        }

        return Ok(new { Message = "Permission revoked from endpoint." });
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLogs()
    {
        var logs = await _db.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .Take(100) // Limit to 100 most recent logs
            .ToListAsync();

        return Ok(logs);
    }
}