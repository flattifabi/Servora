namespace Servora.Installer.Model;

/// <summary>Result of a single request to the <c>/health</c> endpoint.</summary>
internal enum HealthProbeResult
{
    /// <summary>No HTTP response (connection refused, timeout, TLS failure).</summary>
    Unreachable,

    /// <summary>The server answered with a non-success status code (for example 503 Unhealthy).</summary>
    Unhealthy,

    /// <summary>The server answered with a 2xx status code.</summary>
    Healthy,
}
