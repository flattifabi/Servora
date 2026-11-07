namespace Servora.Installer.Model;

/// <summary>Fixed names and defaults of the Servora installation (must match <c>src/Servora.Web/Program.cs</c>).</summary>
internal static class ProductInfo
{
    /// <summary>Product name.</summary>
    public const string ProductName = "Servora";

    /// <summary>Windows service name (used by <c>UseWindowsService</c> in the host).</summary>
    public const string ServiceName = "Servora";

    /// <summary>Display name of the service.</summary>
    public const string ServiceDisplayName = "Servora";

    /// <summary>Description of the service.</summary>
    public const string ServiceDescription =
        "Servora service management platform (web UI, REST API and MCP endpoint).";

    /// <summary>Name of the service executable inside the install directory.</summary>
    public const string ExecutableName = "Servora.exe";

    /// <summary>Environment variable that overrides the data directory (see ADR-011, <c>AppPaths</c>).</summary>
    public const string DataDirectoryVariable = "SERVORA_DATA_DIR";

    /// <summary>Folder name below <c>%ProgramFiles%</c> and <c>%ProgramData%</c>.</summary>
    public const string FolderName = "Servora";

    /// <summary>Folder name of the binary backups below <c>%ProgramData%\Servora</c>.</summary>
    public const string BackupsFolderName = "backups";

    /// <summary>File name of the instance configuration in the data directory.</summary>
    public const string InstanceSettingsFileName = "appsettings.json";

    /// <summary>Name of the local development data folder that must never be removed from an install directory.</summary>
    public const string LegacyDataFolderName = "App_Data";

    /// <summary>Default HTTP port (matches <c>Urls</c> in the shipped <c>appsettings.json</c>).</summary>
    public const int DefaultPort = 5080;

    /// <summary>Name of the inbound Windows Firewall rule.</summary>
    public const string FirewallRuleName = "Servora";

    /// <summary>Logical name of the embedded payload zip resource.</summary>
    public const string PayloadResourceName = "Servora.Payload.zip";

    /// <summary>Folder next to <c>ServoraSetup.exe</c> that may contain the published application.</summary>
    public const string SideBySidePayloadFolder = "app";

    /// <summary>Relative path of the first-run setup wizard.</summary>
    public const string SetupPath = "/setup";

    /// <summary>Relative path of the health endpoint.</summary>
    public const string HealthPath = "/health";

    /// <summary>Default number of binary backups kept after a successful update.</summary>
    public const int DefaultBackupsToKeep = 5;

    /// <summary>Event log source used by the host (application name).</summary>
    public const string EventLogSource = "Servora";
}
