using ECommerceRbac.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;

namespace ECommerceRbac.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;
        if (dbContext == null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        var userIdString = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? userId = Guid.TryParse(userIdString, out var uid) ? uid : null;

        var entries = dbContext.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .Where(e => e.Entity is Role || e.Entity is Permission || e.Entity is Models.Endpoint)
            .ToList();

        foreach (var entry in entries)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = entry.State.ToString(),
                Resource = entry.Entity.GetType().Name,
                Details = $"Record {entry.State}: {entry.Entity.GetType().Name}"
            };
            dbContext.Add(auditLog);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}