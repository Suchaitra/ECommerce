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
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RbacEndpoint("Permission", isSystem: true)]
    public async Task<IActionResult> GetUsers()
    {
        // Adjust "Models.User" to match your actual user entity
        var users = await _db.Users
            .Include(u => u.Roles)
            .Select(u => new
            {
                u.Id,
                u.Email,
                Roles = u.Roles.Select(r => r.Name)
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("{userId}/roles")]
    [RbacEndpoint("Permission", isSystem: true)]
    public async Task<IActionResult> AssignRoleToUser(Guid userId, [FromBody] Guid roleId)
    {
        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound(new { Message = "User not found." });

        var role = await _db.Roles.FindAsync(roleId);
        if (role == null) return NotFound(new { Message = "Role not found." });

        if (!user.Roles.Any(r => r.Id == role.Id))
        {
            user.Roles.Add(role);
            await _db.SaveChangesAsync();
        }

        return Ok(new { Message = "Role assigned to user successfully." });
    }
}