namespace Servora.Installer.Model;

/// <summary>Timeouts and retry settings of the installer workflows (shortened by unit tests).</summary>
internal sealed record InstallerTimings
{
    /// <summary>Gets the default timings used in production.</summary>
    public static InstallerTimings Default { get; } = new();

    /// <summary>Gets the maximum time to wait for the service to start or stop.</summary>
    public TimeSpan ServiceTimeout { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>Gets the maximum time to wait for <c>/health</c> after the service started.</summary>
    public TimeSpan HealthTimeout { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>Gets the delay between two health probes.</summary>
    public TimeSpan HealthInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets the number of attempts for file operations that may hit a briefly locked file.</summary>
    public int FileRetryCount { get; init; } = 10;

    /// <summary>Gets the delay between two file operation attempts.</summary>
    public TimeSpan FileRetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);
}
