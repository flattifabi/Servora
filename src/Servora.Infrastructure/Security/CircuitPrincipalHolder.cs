using System.Security.Claims;

namespace Servora.Infrastructure.Security;

/// <summary>
/// Scoped holder of the authenticated principal of a Blazor Server circuit. Inside a circuit there is no
/// <c>HttpContext</c>, so the circuit handler copies the authentication state here (see ASP.NET Core docs:
/// "Access server-side Blazor services from a different DI scope").
/// </summary>
public sealed class CircuitPrincipalHolder
{
    /// <summary>Gets or sets the circuit principal.</summary>
    public ClaimsPrincipal? Principal { get; set; }

    /// <summary>Gets or sets the remote IP address captured when the circuit was established.</summary>
    public string? IpAddress { get; set; }
}
