using System.Runtime.Versioning;
using Microsoft.Win32;
using Servora.Installer.Abstractions;

namespace Servora.Installer.Platform;

/// <summary>
/// Registers the event source under <c>HKLM\SYSTEM\CurrentControlSet\Services\EventLog\Application</c>. The message
/// file is the <c>System.Diagnostics.EventLog.Messages.dll</c> shipped with the application, or the .NET Framework
/// <c>EventLogMessages.dll</c> present on every supported Windows version.
/// </summary>
/// <param name="environment">The environment.</param>
[SupportedOSPlatform("windows")]
internal sealed class WindowsEventLogRegistrar(IInstallerEnvironment environment) : IEventLogRegistrar
{
    private const string ApplicationLogKey = @"SYSTEM\CurrentControlSet\Services\EventLog\Application";

    /// <inheritdoc />
    public void Register(string source, string installDirectory)
    {
        var messageFile = Path.Combine(installDirectory, "System.Diagnostics.EventLog.Messages.dll");
        if (!File.Exists(messageFile))
        {
            messageFile = Path.Combine(environment.WindowsDirectory, "Microsoft.NET", "Framework64", "v4.0.30319", "EventLogMessages.dll");
        }

        using var key = Registry.LocalMachine.CreateSubKey(Path.Combine(ApplicationLogKey, source), writable: true);
        key.SetValue("EventMessageFile", messageFile, RegistryValueKind.ExpandString);
    }

    /// <inheritdoc />
    public void Unregister(string source)
    {
        using var log = Registry.LocalMachine.OpenSubKey(ApplicationLogKey, writable: true);
        log?.DeleteSubKeyTree(source, throwOnMissingSubKey: false);
    }
}
