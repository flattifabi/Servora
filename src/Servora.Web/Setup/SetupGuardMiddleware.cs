using Servora.Infrastructure.Persistence;

namespace Servora.Web.Setup;

/// <summary>
/// Restricts the application to the setup wizard while no database is configured, and blocks the setup wizard once
/// setup has completed. Static assets and the Blazor framework endpoints are always allowed.
/// </summary>
/// <param name="next">The next middleware.</param>
/// <param name="settingsStore">The database settings store.</param>
public sealed class SetupGuardMiddleware(RequestDelegate next, DatabaseSettingsStore settingsStore)
{
    /// <summary>The setup wizard route.</summary>
    public const string SetupPath = "/setup";

    private static readonly string[] s_alwaysAllowed = ["/_framework", "/_blazor", "/_content", "/css", "/js", "/favicon", "/health"];

    /// <summary>Processes the request.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that completes when the pipeline is done.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (s_alwaysAllowed.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) || Path.HasExtension(path))
        {
            return next(context);
        }

        var isSetupPath = path.StartsWith(SetupPath, StringComparison.OrdinalIgnoreCase);
        if (!settingsStore.IsConfigured && !isSetupPath)
        {
            if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/mcp", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return Task.CompletedTask;
            }

            context.Response.Redirect(SetupPath);
            return Task.CompletedTask;
        }

        if (settingsStore.IsConfigured && isSetupPath && !SetupState.IsInProgress)
        {
            context.Response.Redirect("/");
            return Task.CompletedTask;
        }

        return next(context);
    }
}
