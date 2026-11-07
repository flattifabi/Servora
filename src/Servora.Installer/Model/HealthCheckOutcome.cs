namespace Servora.Installer.Model;

/// <summary>Outcome of waiting for the service to become healthy.</summary>
internal enum HealthCheckOutcome
{
    /// <summary>The health endpoint reported success within the timeout.</summary>
    Healthy,

    /// <summary>The service stopped (crashed) while waiting.</summary>
    ServiceStopped,

    /// <summary>The timeout elapsed without a successful health response.</summary>
    TimedOut,
}
