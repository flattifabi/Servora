namespace Servora.Installer.Cli;

/// <summary>Usage text of <c>ServoraSetup.exe</c>.</summary>
internal static class HelpText
{
    /// <summary>Gets the usage text.</summary>
    public const string Usage = """
        Usage: ServoraSetup.exe [command] [options]

        Commands:
          (none)             Interactive menu. With --silent: install, or update when Servora is already installed.
          install            Install Servora as the Windows service "Servora".
          update             Update an existing installation in place (backup, health check, automatic rollback).
          uninstall          Remove the service and program files. Deletes the data directory unless --keep-data.
          status             Show installation and service status (no administrator rights needed).
          help               Show this help.
          version            Show installer and payload version.

        Options:
          --silent, -s             Unattended: no prompts, defaults are used, no browser is opened.
          --port <n>               HTTP port for a new installation (default 5080, or the configured port).
          --install-dir <path>     Install directory (default %ProgramFiles%\Servora).
          --data-dir <path>        Custom data directory (sets SERVORA_DATA_DIR for the service).
          --service-account <a>    virtual (default, NT SERVICE\Servora) | localsystem | networkservice.
          --firewall               Create an inbound firewall rule for the port (domain/private profiles).
          --no-firewall            Do not create a firewall rule (default in silent mode).
          --no-browser             Do not open the setup wizard in the browser after installation.
          --keep-data              uninstall: keep the data directory (database, keys, attachments, backups).
          --keep-backups <n>       update: number of binary backups to keep (default 5).
          --health-timeout <s>     Seconds to wait for /health after start (default 120).
          --health-url <url>       Health URL to probe (default derived from the configured Urls).
          --source <path>          Install from a published folder or a Servora-<version>-win-x64.zip.
          --force                  Allow downgrades and an occupied port.

        Exit codes:
          0 success, 1 failed, 2 invalid arguments, 3 elevation required, 4 not installed,
          5 already installed, 6 payload missing, 7 update failed and was rolled back, 8 cancelled.

        Examples:
          ServoraSetup.exe
          ServoraSetup.exe install --silent --port 8080 --firewall
          ServoraSetup.exe update --silent
          ServoraSetup.exe uninstall --keep-data
          ServoraSetup.exe status

        Documentation: docs/operations/installation.md
        """;
}
