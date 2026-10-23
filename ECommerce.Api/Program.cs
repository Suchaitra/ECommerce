using ECommerceRbac.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Security.Claims;
using ECommerceRbac.Interceptors;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuditInterceptor>();
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var interceptor = sp.GetRequiredService<AuditInterceptor>();
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddInterceptors(interceptor);
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AuthSession";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };

        options.Events.OnValidatePrincipal = async context =>
        {
            var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var sessionIdClaim = context.Principal?.FindFirst("SessionId")?.Value;

            if (Guid.TryParse(sessionIdClaim, out Guid sessionId))
            {
                var session = await dbContext.Sessions.FindAsync(sessionId);
                if (session == null || session.IsRevoked || session.ExpiresAt < DateTime.UtcNow)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                }
            }
            else
            {
                context.RejectPrincipal();
            }
        };
    });

builder.Services.AddAuthorization();

// JSON Cycle Fix Applied Here
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ECommerceRbac.Filters.RbacAuthorizationFilter>();
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

builder.Services.AddOpenApi();

builder.Services.AddSingleton<ECommerceRbac.Services.EndpointScannerService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await ECommerceRbac.Data.DatabaseSeeder.SeedAsync(db);
}

var scanner = app.Services.GetRequiredService<ECommerceRbac.Services.EndpointScannerService>();
await scanner.ScanEndpointsAsync();

app.MapControllers();
app.Run();