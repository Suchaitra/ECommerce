using ECommerceRbac.Attributes;
using ECommerceRbac.Data;
using ECommerceRbac.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ECommerceRbac.Controllers; // Fixed capital C

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<User> _passwordHasher;

    public AuthController(AppDbContext db)
    {
        _db = db;
        _passwordHasher = new PasswordHasher<User>();
    }

    public record AuthRequest(string Email, string Password);

    [HttpPost("register")]
    [RbacEndpoint("Public")]
    public async Task<IActionResult> Register(AuthRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest("Email already exists");

        var user = new User { Email = request.Email, PasswordHash = "" };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new { user.Id, user.Email });
    }

    [HttpPost("login")]
    [RbacEndpoint("Public")]
    public async Task<IActionResult> Login(AuthRequest request)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized("Invalid credentials");

        var session = new Session
        {
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("SessionId", session.Id.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = session.ExpiresAt
        });

        return Ok(new { Message = "Logged in successfully" });
    }

    [HttpPost("logout")]
    [Authorize]
    [RbacEndpoint("Login")]
    public async Task<IActionResult> Logout()
    {
        var sessionIdClaim = User.FindFirst("SessionId")?.Value;
        if (Guid.TryParse(sessionIdClaim, out Guid sessionId))
        {
            var session = await _db.Sessions.FindAsync(sessionId);
            if (session != null)
            {
                session.IsRevoked = true;
                await _db.SaveChangesAsync();
            }
        }

        await HttpContext.SignOutAsync();
        return Ok(new { Message = "Logged out successfully" });
    }

    [HttpGet("me")]
    [Authorize]
    [RbacEndpoint("Login")]
    public async Task<IActionResult> Me()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var userId)) return Unauthorized();

        var user = await _db.Users
            .Include(u => u.Roles)
            .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return Unauthorized();

        var roles = user.Roles.Select(r => r.Name).ToList();
        var permissions = user.Roles.SelectMany(r => r.Permissions.Select(p => p.Code)).Distinct().ToList();

        return Ok(new
        {
            Id = user.Id,
            Email = user.Email,
            Roles = roles,
            Permissions = permissions
        });
    }
}