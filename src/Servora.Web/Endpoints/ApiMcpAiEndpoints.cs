using Microsoft.AspNetCore.Diagnostics;
using Servora.Application.Common;
using Servora.Application.Mcp;
using Servora.Application.Security;
using Servora.Infrastructure.Persistence;
using Servora.Infrastructure.Security;
using Servora.Web.Api;
using Servora.Web.Mcp;
using Servora.Web.Security;

namespace Servora.Web.Endpoints;

/// <summary>HTTP endpoints of the T-10 API, MCP &amp; AI module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class ApiMcpAiEndpoints
{
    /// <summary>Maps the module endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapApiMcpAiEndpoints(this IEndpointRouteBuilder app)
    {
        // OpenAPI document of the REST API (/openapi/v1.json), for signed-in users and API keys.
        app.MapOpenApi().RequireAuthorization();

        var api = app.MapGroup("/api/v1").WithTags("Platform").AddEndpointFilter(async (context, next) =>
        {
            // API responses must stay JSON: never re-execute the HTML status code pages.
            if (context.HttpContext.Features.Get<IStatusCodePagesFeature>() is { } feature)
            {
                feature.Enabled = false;
            }

            return await next(context);
        });

        api.MapGet("/me", GetMe)
            .RequireAuthorization(PermissionPolicy.For(Permissions.Api.Access))
            .WithName("GetMe")
            .WithSummary("Who am I")
            .WithDescription("Returns the calling identity: the API key (id, scopes) and the user it acts for. Requires the scope api.access.")
            .Produces<MeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        api.MapGet("/health", GetHealthAsync)
            .AllowAnonymous()
            .WithName("GetHealth")
            .WithSummary("Health check")
            .WithDescription("Anonymous liveness and database reachability check for load balancers and monitoring.")
            .Produces<HealthResponse>()
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        app.MapFallback("/api/{**path}", (HttpContext context) =>
            {
                if (context.Features.Get<IStatusCodePagesFeature>() is { } feature)
                {
                    feature.Enabled = false;
                }

                return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: "No API endpoint matches the request path.");
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        // MCP server (Streamable HTTP). API keys only; tool scopes and permissions are checked per call.
        app.MapPost("/mcp", McpHttpEndpoint.HandlePostAsync)
            .RequireAuthorization(ApiKeyAuthenticationDefaults.McpPolicy)
            .DisableAntiforgery()
            .ExcludeFromDescription();
        app.MapMethods("/mcp", [HttpMethods.Get, HttpMethods.Delete], McpHttpEndpoint.MethodNotAllowed)
            .AllowAnonymous()
            .ExcludeFromDescription();
        return app;
    }

    private static IResult GetMe(HttpContext context, ICurrentUser currentUser)
    {
        var scopes = currentUser.ActorType == ActorType.ApiKey
            ? context.User.FindAll(ServoraClaimTypes.Permission).Select(c => c.Value).Order(StringComparer.Ordinal).ToList()
            : [];
        return Results.Ok(new MeResponse(
            currentUser.UserId,
            currentUser.Name,
            currentUser.DisplayName,
            currentUser.ActorType.ToString(),
            currentUser.ApiKeyId,
            scopes,
            currentUser.CorrelationId));
    }

    private static async Task<IResult> GetHealthAsync(DatabaseSettingsStore settings, IAppDbContextFactory dbFactory, IClock clock, CancellationToken cancellationToken)
    {
        if (!settings.IsConfigured)
        {
            return Results.Json(new HealthResponse("setup", McpProtocol.ServerVersion, "not_configured", clock.UtcNow), statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        bool reachable;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            reachable = await db.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            reachable = false;
        }

        var response = new HealthResponse(reachable ? "healthy" : "degraded", McpProtocol.ServerVersion, reachable ? "reachable" : "unreachable", clock.UtcNow);
        return reachable ? Results.Ok(response) : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
