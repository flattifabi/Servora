using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Servora.Application.Security;
using Servora.Web.Api;
using Servora.Web.Security;

namespace Servora.Web.Modules;

/// <summary>Host-level registrations (authentication schemes, logging providers, options) of the T-10 API, MCP &amp; AI module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class ApiMcpAiWebModule
{
    /// <summary>Applies the module's host configuration.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder.</returns>
    public static WebApplicationBuilder AddApiMcpAiWeb(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        // Authentication: the ApiKey scheme plus a policy scheme that becomes the default and forwards API paths with a key
        // to the ApiKey scheme and everything else to the cookie scheme registered in Program.cs.
        services.AddAuthentication()
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationDefaults.Scheme, "API key", _ => { })
            .AddPolicyScheme(ApiKeyAuthenticationDefaults.SelectorScheme, "Cookie or API key", options => options.ForwardDefaultSelector = ApiSchemeSelector.Select);
        services.Configure<AuthenticationOptions>(options => options.DefaultScheme = ApiKeyAuthenticationDefaults.SelectorScheme);

        // The MCP endpoint accepts API keys only (no cookies → no CSRF) and requires mcp.access (scope and owner permission).
        services.AddAuthorization(options => options.AddPolicy(
            ApiKeyAuthenticationDefaults.McpPolicy,
            policy => policy
                .AddAuthenticationSchemes(ApiKeyAuthenticationDefaults.Scheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(Permissions.Mcp.Access))));

        // REST conventions: correlation id + problem details for unhandled errors, per-key rate limits, problem details
        // enriched with the correlation id, OpenAPI document.
        services.AddTransient<IStartupFilter, ApiPipelineStartupFilter>();
        services.Configure<RateLimiterOptions>(ApiRateLimiting.Configure);
        services.Configure<ProblemDetailsOptions>(options =>
        {
            var previous = options.CustomizeProblemDetails;
            options.CustomizeProblemDetails = context =>
            {
                previous?.Invoke(context);
                context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
            };
        });
        services.AddOpenApi("v1", options =>
        {
            options.ShouldInclude = description => description.RelativePath?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true;
            options.AddDocumentTransformer<ServoraOpenApiDocumentTransformer>();
        });
        return builder;
    }
}
