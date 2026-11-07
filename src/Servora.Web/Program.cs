using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting.WindowsServices;
using Servora.Application.Common;
using Servora.Infrastructure.Hosting;
using Servora.Infrastructure.Modules;
using Servora.Infrastructure.Persistence;
using Servora.Persistence.PostgreSql;
using Servora.Persistence.Sqlite;
using Servora.Persistence.SqlServer;
using Servora.Web.Components;
using Servora.Web.Endpoints;
using Servora.Web.Modules;
using Servora.Web.Security;
using Servora.Web.Services;
using Servora.Web.Setup;

// ---------------------------------------------------------------------------------------------------------------
// Servora host. Runs as console app (development) or Windows Service (production, see docs/operations).
// ---------------------------------------------------------------------------------------------------------------
var isWindowsService = WindowsServiceHelpers.IsWindowsService();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = isWindowsService ? AppContext.BaseDirectory : null,
});

builder.Host.UseWindowsService(options => options.ServiceName = "Servora");

var paths = new AppPaths(builder.Environment.ContentRootPath, isWindowsService);
builder.Services.AddSingleton<IAppPaths>(paths);

// Instance-specific overrides (port, TLS certificate, ...) survive upgrades because they live in the data directory.
builder.Configuration.AddJsonFile(Path.Combine(paths.DataDirectory, "appsettings.json"), optional: true, reloadOnChange: true);

// Data Protection: keys outside the database, DPAPI-protected on Windows (ADR-007).
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("Servora")
    .PersistKeysToFileSystem(new DirectoryInfo(paths.KeyDirectory));
if (OperatingSystem.IsWindows())
{
    dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
}

// Application + infrastructure modules and database providers.
builder.Services.AddServoraInfrastructure();
builder.Services.AddSingleton<IDatabaseProvider, SqliteDatabaseProvider>();
builder.Services.AddSingleton<IDatabaseProvider, SqlServerDatabaseProvider>();
builder.Services.AddSingleton<IDatabaseProvider, PostgreSqlDatabaseProvider>();

// Blazor (interactive server).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<CircuitHandler, UserCircuitHandler>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<UiStateService>();

// Authentication & authorization.
builder.Services.AddScoped<SecurityStampValidator>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "servora.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = async context =>
        {
            var settings = context.HttpContext.RequestServices.GetRequiredService<DatabaseSettingsStore>();
            if (context.Principal is null || !settings.IsConfigured)
            {
                return;
            }

            var validator = context.HttpContext.RequestServices.GetRequiredService<SecurityStampValidator>();
            if (!await validator.IsValidAsync(context.Principal, context.HttpContext.RequestAborted))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/mcp"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/mcp"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Abuse protection: credential endpoints are rate limited per client IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

// Module host registrations (authentication schemes, logging providers, OpenAPI, ...).
builder.AddServoraWebModules();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseMiddleware<SetupGuardMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapServoraEndpoints();
app.MapHealthChecks("/health").AllowAnonymous();

var databaseSettings = app.Services.GetRequiredService<DatabaseSettingsStore>();
if (databaseSettings.IsConfigured)
{
    await app.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}
else
{
    app.Logger.LogWarning("No database is configured. Open the web UI to run the setup wizard.");
}

await app.RunAsync();

/// <summary>Entry point type (exposed for integration tests).</summary>
public partial class Program;
