namespace Servora.Installer.Model;

/// <summary>Configuration and state of an installed Windows service.</summary>
/// <param name="Name">The service name.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="State">The current runtime state.</param>
/// <param name="StartMode">The start mode as text (for example <c>Automatic</c>, <c>Manual</c>, <c>Disabled</c>).</param>
/// <param name="DelayedAutoStart">Whether automatic start is delayed.</param>
/// <param name="BinaryPath">The raw image path registered with the Service Control Manager (may be quoted, may contain arguments).</param>
/// <param name="Account">The account the service runs as.</param>
internal sealed record ServiceInfo(
    string Name,
    string DisplayName,
    ServiceState State,
    string StartMode,
    bool DelayedAutoStart,
    string BinaryPath,
    string Account);
