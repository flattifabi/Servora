using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Servora.Web.Setup;

/// <summary>
/// Captures, once per circuit, whether the browser connected from the local machine, so the setup wizard knows whether
/// the setup token is required. Runs before any component of the circuit renders.
/// </summary>
/// <param name="session">The circuit's setup session.</param>
/// <param name="httpContextAccessor">The HTTP context accessor.</param>
public sealed class SetupCircuitHandler(SetupSession session, IHttpContextAccessor httpContextAccessor) : CircuitHandler
{
    /// <inheritdoc />
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (httpContextAccessor.HttpContext is { } context)
        {
            session.CaptureClient(context);
        }

        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }
}
