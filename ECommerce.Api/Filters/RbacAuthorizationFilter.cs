using ECommerceRbac.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ECommerceRbac.Filters;

public class RbacAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly AppDbContext _db;

    public RbacAuthorizationFilter(AppDbContext db)
    {
        _db = db;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var routeTemplate = context.ActionDescriptor.AttributeRouteInfo?.Template?.ToLower() ?? "";
        var route = routeTemplate.StartsWith("/") ? routeTemplate : "/" + routeTemplate;
        var method = context.HttpContext.Request.Method;

        var endpoint = await _db.Endpoints
            .Include(e => e.Permissions)
            .FirstOrDefaultAsync(e => e.Route == route && e.Method == method);

        if (endpoint == null)
        {
            context.Result = new ObjectResult(new { Message = $"Endpoint not registered: {route}" }) { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        if (endpoint.AuthMode == "Public") return;

        // 1. Fetch User Identity early
        var userIdString = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var user = await _db.Users
            .Include(u => u.Roles)
            .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // ADDED FIX: Explicitly allow access if the route only requires a basic login
        if (endpoint.AuthMode == "Login") return;

        // 2. Super Admin Bypass (Processed BEFORE the unmapped lockout)
        if (user.Roles.Any(r => r.IsSystem && r.Name == "Super admin")) return;

        // 3. Deny unmapped endpoints for regular users
        if (endpoint.AuthMode == "Permission" && !endpoint.Permissions.Any())
        {
            context.Result = new ObjectResult(new { Message = "Endpoint locked (unmapped)." }) { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        // 4. Standard RBAC permission evaluation
        var userPermissions = user.Roles.SelectMany(r => r.Permissions.Select(p => p.Code)).Distinct().ToList();
        var requiredPermissions = endpoint.Permissions.Select(p => p.Code).ToList();

        bool hasAccess = endpoint.MatchMode == "ALL"
            ? requiredPermissions.All(rp => userPermissions.Contains(rp))
            : requiredPermissions.Any(rp => userPermissions.Contains(rp));

        if (!hasAccess)
        {
            context.Result = new ObjectResult(new { Message = "Access denied by RBAC." }) { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}