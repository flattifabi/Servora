using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting.WindowsServices;
using Servora.Application.Common;
using Servora.Infrastructure.Backup;
using Servora.Infrastructure.Hosting;
using Servora.Infrastructure.Modules;
using Servora.Infrastructure.Persistence;
using Servora.Persistence.PostgreSql;
using Servora.Persistence.Sqlite;
using Servora.Persistence.SqlServer;
using Servora.Web.Components;
using Servora.Web.Api;
using Servora.Web.Endpoints;
using Servora.Web.Modules;
using Servora.Web.Security;
using Servora.Web.Services;
using Servora.Web.Setup;

// ---------------------------------------------------------------------------------------------------------------
// Servora host. Runs as console app (development) or Windows Service (production, see docs/operations).
// ---------------------------------------------------------------------------------------------------------------
// Disaster-recovery command line (`Servora backup list|create|verify|restore`, see docs/modules/backup.md). It runs
// before the web host starts and selects the data directory: --data-dir, SERVORA_DATA_DIR or the service data directory.
var backupCommand = BackupCommandLine.IsBackupCommand(args) ? BackupCommandLine.Parse(args) : null;
if (backupCommand is not null)
{
    var serviceDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Servora");
    if (!string.IsNullOrWhiteSpace(backupCommand.DataDirectory))
    {
        Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, Path.GetFullPath(backupCommand.DataDirectory));
    }
    else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AppPaths.DataDirectoryVariable)) &&
             File.Exists(Path.Combine(serviceDataDirectory, "database.json")) &&
             !Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "App_Data")))
    {
        Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, serviceDataDirectory);
    }
}

var isWindowsService = backupCommand is null && WindowsServiceHelpers.IsWindowsService();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = backupCommand is null ? args : [],
    ContentRootPath = isWindowsService ? AppContext.BaseDirectory : null,
});
if (backupCommand is not null)
{
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
}

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
builder.Services.AddSingleton<BrandingState>();

// Cookie names carry an instance suffix so several instances on one host (different ports) do not clash.
var cookieNames = new InstanceCookieNames(paths.DataDirectory);
builder.Services.AddSingleton(cookieNames);
builder.Services.AddAntiforgery(options => options.Cookie.Name = cookieNames.Antiforgery);

// Authentication & authorization.
builder.Services.AddScoped<SecurityStampValidator>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = cookieNames.Authentication;
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
        options.Events.OnSignedIn = UserThemeCookie.OnSignedInAsync;

        // API callers (also browser calls carrying the cookie) get problem details instead of HTML sign-in redirects.
        options.Events.OnRedirectToLogin = ApiAwareCookieRedirects.RedirectToLoginAsync;
        options.Events.OnRedirectToAccessDenied = ApiAwareCookieRedirects.RedirectToAccessDeniedAsync;
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

if (backupCommand is not null)
{
    try
    {
        return await BackupCommandLine.RunAsync(app.Services, backupCommand, Console.In, Console.Out);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return BackupCommandLine.ExitFailure;
    }
}

// API paths (/api, /mcp, /openapi) never get HTML: unhandled exceptions become problem details in
// ApiCorrelationMiddleware and empty error responses get a problem body. Only UI paths re-execute the error / not-found
// pages (re-executing a POST against a Razor component page also failed with "400 invalid content type").
if (!app.Environment.IsDevelopment())
{
    app.UseWhen(context => !ApiProblemWriter.IsApiRequest(context), ui => ui.UseExceptionHandler("/error", createScopeForErrors: true));
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseWhen(ApiProblemWriter.IsApiRequest, api => api.UseStatusCodePages(ApiProblemWriter.WriteStatusCodeAsync));
app.UseWhen(context => !ApiProblemWriter.IsApiRequest(context), ui => ui.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
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
    // A restore requested in the UI or on the command line runs first, before migrations (ADR-015).
    await app.Services.GetRequiredService<PendingRestoreProcessor>().RunAsync();
    await app.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    await app.Services.GetRequiredService<BrandingState>().WarmUpAsync(app.Services);
}
else
{
    app.Logger.LogWarning("No database is configured. Open the web UI to run the setup wizard.");
}

await app.RunAsync();

// Non-zero after a restore request so the Windows service recovery restarts Servora (see BackupWebModule).
return Environment.ExitCode;

/// <summary>Entry point type (exposed for integration tests).</summary>
public partial class Program;
