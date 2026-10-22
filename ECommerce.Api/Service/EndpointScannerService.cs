using ECommerceRbac.Attributes;
using ECommerceRbac.Data;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ECommerceRbac.Services;

public class EndpointScannerService
{
    private readonly IActionDescriptorCollectionProvider _actionDescriptorProvider;
    private readonly IServiceScopeFactory _scopeFactory;

    public EndpointScannerService(IActionDescriptorCollectionProvider actionDescriptorProvider, IServiceScopeFactory scopeFactory)
    {
        _actionDescriptorProvider = actionDescriptorProvider;
        _scopeFactory = scopeFactory;
    }

    public async Task ScanEndpointsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var routes = _actionDescriptorProvider.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Select(ad => new
            {
                Method = ad.ActionConstraints?.OfType<HttpMethodActionConstraint>().FirstOrDefault()?.HttpMethods.FirstOrDefault() ?? "GET",
                Route = "/" + ad.AttributeRouteInfo?.Template?.ToLower(),
                Attribute = ad.MethodInfo.GetCustomAttributes(typeof(RbacEndpointAttribute), true).FirstOrDefault() as RbacEndpointAttribute
                            ?? ad.ControllerTypeInfo.GetCustomAttributes(typeof(RbacEndpointAttribute), true).FirstOrDefault() as RbacEndpointAttribute
            })
            .Where(x => x.Route != null && x.Attribute != null)
            .ToList();

        var existingEndpoints = await db.Endpoints.ToListAsync();

        foreach (var route in routes)
        {
            var exists = existingEndpoints.FirstOrDefault(e => e.Method == route.Method && e.Route == route.Route);
            if (exists == null)
            {
                db.Endpoints.Add(new Models.Endpoint
                {
                    Method = route.Method,
                    Route = route.Route!,
                    AuthMode = route.Attribute!.AuthMode,
                    IsSystem = route.Attribute.IsSystem,
                    MatchMode = "ANY"
                });
            }
        }

        await db.SaveChangesAsync();
    }
}