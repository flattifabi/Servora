namespace Servora.Domain.Identity;

/// <summary>Outcome of a <see cref="DirectorySyncRun"/>.</summary>
public enum DirectorySyncStatus
{
    /// <summary>The run is in progress.</summary>
    Running = 0,

    /// <summary>All entries were synchronised.</summary>
    Succeeded = 1,

    /// <summary>The run completed but some entries failed or the result was truncated.</summary>
    CompletedWithWarnings = 2,

    /// <summary>The run failed (for example the directory was unreachable).</summary>
    Failed = 3,
}
