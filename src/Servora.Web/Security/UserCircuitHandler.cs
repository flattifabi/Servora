using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Servora.Infrastructure.Security;

namespace Servora.Web.Security;

/// <summary>
/// Copies the circuit's authentication state into <see cref="CircuitPrincipalHolder"/> before every inbound activity,
/// so scoped services such as <c>ICurrentUser</c> know the user inside interactive components.
/// </summary>
/// <param name="authenticationStateProvider">The authentication state provider.</param>
/// <param name="holder">The circuit principal holder.</param>
/// <param name="httpContextAccessor">The HTTP context accessor (used once to capture the client IP).</param>
public sealed class UserCircuitHandler(
    AuthenticationStateProvider authenticationStateProvider,
    CircuitPrincipalHolder holder,
    IHttpContextAccessor httpContextAccessor) : CircuitHandler, IDisposable
{
    /// <inheritdoc />
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        authenticationStateProvider.AuthenticationStateChanged += OnAuthenticationChanged;
        holder.IpAddress ??= httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    /// <inheritdoc />
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next) =>
        async context =>
        {
            holder.Principal = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
            await next(context);
        };

    /// <inheritdoc />
    public void Dispose() => authenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationChanged;

    private void OnAuthenticationChanged(Task<AuthenticationState> task) =>
        _ = task.ContinueWith(t => holder.Principal = t.Result.User, TaskContinuationOptions.OnlyOnRanToCompletion);
}
