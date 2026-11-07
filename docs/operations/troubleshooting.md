# Troubleshooting

Start every investigation with:

```powershell
.\ServoraSetup.exe status          # no admin rights needed
Get-WinEvent -FilterHashtable @{ LogName = 'Application'; ProviderName = 'Servora', '.NET Runtime', 'Application Error' } -MaxEvents 20 |
    Format-List TimeCreated, ProviderName, LevelDisplayName, Message
Get-ChildItem C:\ProgramData\Servora\logs | Sort-Object LastWriteTime -Descending | Select-Object -First 5
```

| Where | What you find |
|-------|---------------|
| `ServoraSetup status` | Service state, installed version, data directory, listen URLs, health, backups |
| Windows Event Log (Application, source `Servora`) | Startup failures, unhandled exceptions, warnings |
| `<data dir>\logs\` | Application logs (enable the debug switch in the UI for more detail) |
| `<data dir>\logs\setup-*.log` | Transcript of every elevated installer run |

## Installer

| Symptom | Cause / fix |
|---------|-------------|
| Exit code 3, "Administrator rights are required" | Run from an elevated prompt. In silent mode the installer never shows a UAC prompt. |
| Exit code 5, "already installed" | Use `update` (or run without a command and `--silent` for install-or-update). |
| Exit code 4, "not installed" | `update`/`uninstall` need the service `Servora`. Check `Get-Service Servora`. |
| Exit code 6, "No application payload found" | You are running a developer build without embedded payload. Use the release `ServoraSetup-<version>-win-x64.exe`, an `app` folder next to the exe, or `--source <zip or folder>`. |
| "Port 5080 is already in use" | Find the process: `Get-NetTCPConnection -LocalPort 5080 -State Listen | Select-Object OwningProcess` then `Get-Process -Id <pid>`. Choose another port (`--port`) or stop the other program. |
| "already contains other files" | The install directory must be empty, new, or contain a previous Servora installation. |
| "The payload is older than the installed version" | Downgrades are blocked because the database schema may already be newer. See [manual rollback](upgrade.md#manual-rollback). |
| SmartScreen "Windows protected your PC" | The installer is not code-signed yet. Verify the SHA-256 checksum from `SHA256SUMS.txt` first, then "More info" → "Run anyway". |
| Antivirus quarantines or slows the installer | Self-extracting single-file executables are sometimes flagged heuristically. Verify the checksum and allow the file, or use the zip and the [manual installation](installation.md#manual-installation-from-the-zip). |

## An update was rolled back

Exit code 7 means the new version did not start or did not answer `/health` in time, and the previous version was
restored automatically.

1. Read the transcript `<data dir>\logs\setup-<timestamp>.log` – it names the failing step.
2. Check the Event Log and application logs from the time of the attempt (startup exceptions, failed database
   migration, invalid configuration).
3. Slow first start (large database migration)? Retry with a longer timeout: `update --health-timeout 600`.
4. HTTPS-only configuration? The derived health URL may not match the certificate; pass
   `--health-url https://servora.example.com/health`.
5. If the rollback itself failed (exit code 1, "Rollback failed"), restore manually from the backup folder named in
   the output – see [upgrade.md](upgrade.md#manual-rollback).

## Service does not start

| Symptom | Cause / fix |
|---------|-------------|
| Starts and stops immediately; Event Log shows "address already in use" | Another process uses the port – see above – or two endpoints use the same port. |
| `UnauthorizedAccessException` for `C:\ProgramData\Servora\...` | The service account has no access to the data directory, typically after changing the service account. Grant it: `icacls C:\ProgramData\Servora /grant "<account>:(OI)(CI)M"`. |
| Invalid JSON / duplicate key in configuration | `<data dir>\appsettings.json` contains a syntax error or the same key twice (keys are case-insensitive). Validate with `Get-Content C:\ProgramData\Servora\appsettings.json -Raw | ConvertFrom-Json`. |
| Certificate errors at startup ("The certificate ... could not be found" / "no private key") | Check `Kestrel:Endpoints:*:Certificate` (subject, store, location) and that `NT SERVICE\Servora` can read the private key – see [configuration.md](configuration.md#https-with-kestrel). |
| Error 1053 "did not respond to the start request in a timely fashion" | Usually an exception during startup; run the host from a console to see it ([windows-service.md](windows-service.md#running-from-the-console-diagnostics)). |
| "The service did not start due to a logon failure" | Password of a domain service account expired or changed. Use the virtual account or a gMSA. |
| Database connection errors after moving to another server | Data Protection keys are machine-bound; reconfigure the database connection and re-enter secrets ([backup-restore.md](backup-restore.md#about-the-data-protection-keys-important)). |

## Web UI not reachable

1. On the server: `Invoke-WebRequest http://localhost:5080/health -UseBasicParsing`. If this fails, the service is not
   running or listens on a different URL (`ServoraSetup status` → *Listen URLs*).
2. From another machine: is the firewall rule present and covering the right port and network profile?
   `Get-NetFirewallRule -DisplayName Servora | Get-NetFirewallPortFilter`. The installer's rule allows the *domain* and
   *private* profiles only; a server whose network is classified as *public* blocks it.
3. `Urls` bound to `127.0.0.1` only accepts local connections (intended behind a reverse proxy).
4. "400 Bad Request – Invalid Hostname": the host name is not listed in `AllowedHosts`.
5. The UI loads but shows "Attempting to reconnect" repeatedly: the reverse proxy does not forward WebSockets or closes
   idle connections too early – see [configuration.md](configuration.md#reverse-proxy).

## Setup wizard

| Symptom | Cause / fix |
|---------|-------------|
| Wizard asks for a setup token | You opened it from another machine. Open it on the server (`http://localhost:<port>/setup`) or use the token from `<data dir>\setup-token.txt`. |
| Every page redirects to `/setup` | No database is configured yet (`database.json` missing). Complete the wizard. |
| `/setup` redirects to the start page | Setup has already been completed. Database settings are changed in the administration UI afterwards. |

## Uninstall

| Symptom | Cause / fix |
|---------|-------------|
| Service still listed after uninstall ("marked for deletion") | A program holds a handle to the service (Services console, Event Viewer, Task Manager). Close them; the entry disappears, at the latest after a reboot. |
| Install directory not deleted | `ServoraSetup.exe` was started from inside the install directory, or a file is locked (e.g. an Explorer window, antivirus scan). Delete the folder manually. |

## Collecting information for a bug report

```powershell
.\ServoraSetup.exe status > servora-status.txt
Get-WinEvent -FilterHashtable @{ LogName = 'Application'; ProviderName = 'Servora' } -MaxEvents 50 | Format-List > servora-events.txt
```

Attach both files and the relevant part of the newest log in `<data dir>\logs`. Review them before sharing – never
attach `database.json`, the `keys` folder, PFX files or connection strings.
