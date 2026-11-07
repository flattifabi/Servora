using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Servora.Application.Api;
using Servora.Infrastructure.Security;

namespace Servora.Web.Api;

/// <summary>
/// Authentication handler of the <c>ApiKey</c> scheme. Produces a principal with <c>actor_type=apikey</c>, the key id,
/// one permission claim per scope and – for user-owned keys – the owner's user id as <see cref="ClaimTypes.NameIdentifier"/>
/// (so that every permission check also requires the owner to hold the permission).
/// </summary>
/// <param name="options">The options monitor.</param>
/// <param name="logger">The logger factory.</param>
/// <param name="encoder">The URL encoder.</param>
/// <param name="authenticator">The key authenticator.</param>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyAuthenticator authenticator) : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    /// <summary>Builds the principal of an authenticated key.</summary>
    /// <param name="identity">The key identity.</param>
    /// <param name="scheme">The scheme name.</param>
    /// <returns>The principal.</returns>
    public static ClaimsPrincipal CreatePrincipal(ApiKeyIdentity identity, string scheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, identity.PrincipalName),
            new(ServoraClaimTypes.DisplayName, identity.DisplayName),
            new(ServoraClaimTypes.ActorType, "apikey"),
            new(ServoraClaimTypes.ApiKeyId, identity.Id.ToString()),
            new(ServoraClaimTypes.AuthSource, "apikey"),
        };

        if (identity.OwnerUserId is { } ownerId)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, ownerId.ToString()));
        }

        claims.AddRange(identity.Scopes.Select(scope => new Claim(ServoraClaimTypes.Permission, scope)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme, ClaimTypes.Name, ClaimTypes.Role));
    }

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ApiCredentialReader.ReadToken(Request);
        if (token is null)
        {
            return AuthenticateResult.NoResult();
        }

        var result = await authenticator.AuthenticateAsync(token, Context.Connection.RemoteIpAddress, Context.RequestAborted);
        if (!result.Succeeded)
        {
            Context.Items[ApiKeyAuthenticationDefaults.FailureItemKey] = result.Failure;
            return AuthenticateResult.Fail(result.Failure ?? ApiKeyAuthenticator.InvalidKeyMessage);
        }

        return AuthenticateResult.Success(new AuthenticationTicket(CreatePrincipal(result.Identity!, Scheme.Name), Scheme.Name));
    }

    /// <inheritdoc />
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"Bearer realm=\"{Options.Realm}\"";
        var failure = Context.Items[ApiKeyAuthenticationDefaults.FailureItemKey] as string;
        await WriteProblemAsync(
            StatusCodes.Status401Unauthorized,
            "Unauthorized",
            failure ?? $"An API key is required. Send it in the '{ApiKeyAuthenticationDefaults.HeaderName}' header or as 'Authorization: Bearer srv_…'.");
    }

    /// <inheritdoc />
    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await WriteProblemAsync(
            StatusCodes.Status403Forbidden,
            "Forbidden",
            "The API key lacks the required scope, or the key owner lacks the required permission.");
    }

    private async Task WriteProblemAsync(int status, string title, string detail)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail, Type = $"https://httpstatuses.io/{status}" };
        var service = Context.RequestServices.GetService<IProblemDetailsService>();
        if (service is null || !await service.TryWriteAsync(new ProblemDetailsContext { HttpContext = Context, ProblemDetails = problem }))
        {
            await Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
        }
    }
}
