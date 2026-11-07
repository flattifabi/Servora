using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace Servora.Web.Security;

/// <summary>
/// Re-validates the user of every Blazor circuit once a minute. When the user was deactivated or the security stamp
/// changed, the circuit loses its authentication state and the UI redirects to the login page.
/// </summary>
/// <param name="loggerFactory">The logger factory.</param>
/// <param name="scopeFactory">The scope factory.</param>
public sealed class RevalidatingAuthenticationStateProvider(ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    /// <inheritdoc />
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var validator = scope.ServiceProvider.GetRequiredService<SecurityStampValidator>();
        return await validator.IsValidAsync(authenticationState.User, cancellationToken);
    }
}
