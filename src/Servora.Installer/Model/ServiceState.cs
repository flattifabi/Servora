namespace Servora.Installer.Model;

/// <summary>Runtime state of the Windows service (mirrors the Service Control Manager states).</summary>
internal enum ServiceState
{
    /// <summary>The service is not registered.</summary>
    NotInstalled,

    /// <summary>The service is stopped.</summary>
    Stopped,

    /// <summary>The service is starting.</summary>
    StartPending,

    /// <summary>The service is stopping.</summary>
    StopPending,

    /// <summary>The service is running.</summary>
    Running,

    /// <summary>The service is resuming from a pause.</summary>
    ContinuePending,

    /// <summary>The service is pausing.</summary>
    PausePending,

    /// <summary>The service is paused.</summary>
    Paused,

    /// <summary>The state could not be determined.</summary>
    Unknown,
}
