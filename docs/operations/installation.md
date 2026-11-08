# Installation

Servora runs on Windows as the service **`Servora`**. A release contains everything needed – no .NET runtime or other
download is required on the server.

| Release asset | Purpose |
|---------------|---------|
| `ServoraSetup-<version>-win-x64.exe` | Installer and updater. The published application is embedded in the exe (single download). |
| `Servora-<version>-win-x64.zip` | The published application for manual installation or offline staging. |
| `SHA256SUMS.txt` | SHA-256 checksums of both files. |

Related guides: [upgrade](upgrade.md) · [Windows service](windows-service.md) · [configuration](configuration.md) ·
[backup & restore](backup-restore.md) · [troubleshooting](troubleshooting.md)

## Requirements

- Windows Server 2016 or later, or Windows 10/11 – 64-bit (x64).
- Administrator rights for install, update and uninstall.
- About 400 MB free disk space for program files and backups, plus space for data (database, attachments, logs).
- One free TCP port (default **5080**).
- Optional: SQL Server or PostgreSQL. SQLite needs nothing extra and is the default in the setup wizard.

## 1. Download and verify

Download the installer and `SHA256SUMS.txt` from the GitHub release and compare the checksum:

```powershell
Get-FileHash -Algorithm SHA256 .\ServoraSetup-1.0.0-win-x64.exe
Get-Content .\SHA256SUMS.txt
```

The hash must match the line for the file. The installer is not code-signed yet, so Windows SmartScreen may warn on the
first start ("More info" → "Run anyway") – only do that after the checksum matched.

## 2. Install interactively

Double-click `ServoraSetup-<version>-win-x64.exe` (or run it from a terminal without arguments). It requests
administrator rights through the UAC prompt and shows a menu:

```
  Servora             not installed
  Payload             embedded payload

  What would you like to do?
    1) Install Servora
    2) Show detailed status
    0) Exit
```

Choose **Install** and answer the prompts:

| Prompt | Default | Notes |
|--------|---------|-------|
| Install directory | `C:\Program Files\Servora` | Must be a dedicated folder (empty, new, or an earlier Servora install). |
| HTTP port | `5080` | The installer checks that the port is free. |
| Firewall rule | yes | Inbound TCP rule for the port, **domain and private** profiles only (public networks stay closed). |

After a summary and confirmation the installer copies the files, registers and starts the service, waits until
`/health` answers and opens the **setup wizard** at `http://localhost:<port>/setup` in your browser.

### Complete the setup wizard

The wizard configures the database (SQLite, SQL Server or PostgreSQL), the administrator password and the basic
system settings. Run it **on the server itself** (`localhost`). Opening the wizard from another machine requires the
setup token that Servora writes to `%ProgramData%\Servora\setup-token.txt` while it is unconfigured.

Afterwards sign in with **`admin` / `admin`** – you must choose a new password immediately.

### Start empty or with sample data

A new installation contains only the `admin` account and reference data (roles, incident categories, CI classes,
notification templates, system reports). To try the product with demo users, tickets, articles, assets and a service
catalog, switch on **Install sample data** in the setup wizard.

**Backup & Restore → Reset data** empties an instance later. It requires the permission `backup.restore`, creates a
full backup first, and removes tickets, requests, the service catalog, knowledge, assets and CIs, flows and data
sources, comments, attachments, approvals, notifications, AI conversations, groups and all users except `admin`, the
signed-in administrator and service accounts. Settings, branding, roles, secrets, API keys, SLA definitions,
categories, CI classes, reports and the audit log stay. Record numbers start again at 1001. Roll back with **Restore**.

## 3. Install unattended (silent)

```powershell
# Install with defaults (port 5080, no firewall rule, no browser)
.\ServoraSetup-1.0.0-win-x64.exe install --silent

# Custom port and directory, open the firewall
.\ServoraSetup-1.0.0-win-x64.exe install --silent --port 8080 --install-dir "D:\Servora" --firewall

# Install if missing, otherwise update (ideal for software distribution tools)
.\ServoraSetup-1.0.0-win-x64.exe --silent
```

Silent mode never prompts and never raises a UAC prompt: run it from an elevated session (SYSTEM via your software
distribution tool, an elevated PowerShell, a scheduled task with highest privileges). Check the exit code (below).

To skip the database step of the wizard in automated deployments, pre-configure the database in the
[instance settings](configuration.md#pre-configuring-the-database) before starting the service.

## Command reference

```
ServoraSetup.exe [command] [options]
```

| Command | Description | Elevation |
|---------|-------------|-----------|
| *(none)* | Interactive menu; with `--silent`: install, or update when already installed. | yes |
| `install` | Fresh installation. Fails with exit code 5 when the service exists. | yes |
| `update` | In-place update with backup, health check and automatic rollback – see [upgrade.md](upgrade.md). | yes |
| `uninstall` | Removes service, firewall rule, event source and program files. Deletes the **data directory** unless `--keep-data`. | yes |
| `status` | Service state, versions, directories, URLs, health, backups. | **no** |
| `help`, `version` | Usage / installer and payload version. | no |

| Option | Description |
|--------|-------------|
| `--silent`, `-s` | Unattended: no prompts (defaults are used), no browser, no UAC prompt. |
| `--port <n>` | HTTP port of a new installation. Default: the port already configured in the instance settings, else 5080. |
| `--install-dir <path>` | Install directory (default `%ProgramFiles%\Servora`). Ignored by `update`. |
| `--data-dir <path>` | Custom data directory; stored as service environment variable `SERVORA_DATA_DIR`. |
| `--service-account <a>` | `virtual` (default, `NT SERVICE\Servora`), `localsystem` or `networkservice` – see [windows-service.md](windows-service.md#service-account). |
| `--firewall` / `--no-firewall` | Create / do not create the inbound firewall rule (silent default: no rule). |
| `--no-browser` | Do not open the setup wizard after installation. |
| `--keep-data` | `uninstall`: keep the data directory. |
| `--keep-backups <n>` | `update`: number of binary backups to keep (default 5). |
| `--health-timeout <s>` | Seconds to wait for `/health` after start (default 120). |
| `--health-url <url>` | URL to probe instead of the one derived from the configuration (HTTPS-only setups). |
| `--source <path>` | Use a published folder or a `Servora-<version>-win-x64.zip` instead of the embedded payload. |
| `--force` | Allow downgrades and an occupied port. |

### Exit codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Failed (the machine was left unchanged or reverted; see the output) |
| 2 | Invalid command line |
| 3 | Administrator rights required |
| 4 | Servora is not installed (`update`, `uninstall`) |
| 5 | Servora is already installed (`install`) |
| 6 | No payload found / invalid payload |
| 7 | Update failed and the previous version was restored |
| 8 | Cancelled by the operator (prompt or UAC) |

Every elevated run writes a transcript to `%ProgramData%\Servora\logs\setup-<timestamp>.log`.

## What the installer does

1. Extracts the payload to a temporary folder and verifies that it contains `Servora.exe` – before touching anything.
2. Copies the program files to the install directory and restricts its ACL to SYSTEM and Administrators (full
   control) and Users (read & execute), so nobody else can replace binaries that run as a service.
3. Writes `Urls` with the chosen port into the instance settings `<data dir>\appsettings.json` (other keys in an
   existing file are preserved).
4. Registers the service: delayed automatic start, quoted executable path, recovery actions *restart after 1 min /
   1 min / 5 min*, failure counter reset after 24 h, also on non-crash failures.
5. Restricts the **data directory** ACL to SYSTEM, Administrators and the service account – the database, encryption
   keys, attachments and logs are not readable by other local users.
6. Registers the event log source `Servora` (Application log).
7. Optionally creates the firewall rule `Servora`.
8. Starts the service and polls `/health` (on a fresh installation any HTTP answer counts, because dependency checks may
   report "unhealthy" until the wizard configured the database).
9. Opens the setup wizard (interactive mode).

If any step fails, the installer reverts steps 2–8 (service, firewall rule, event source, program files). The data
directory is left in place.

## Payload: embedded zip, `app` folder or `--source`

The release workflow embeds `Servora-<version>-win-x64.zip` into `ServoraSetup.exe` as a resource, so a single file
is enough. The installer looks for a payload in this order:

1. `--source <folder or zip>`
2. the embedded zip (release builds)
3. an `app` folder next to `ServoraSetup.exe` that contains `Servora.exe` (developer builds, custom packaging)

Example – install a locally built application with a development build of the installer:

```powershell
dotnet publish src/Servora.Web -c Release -r win-x64 --self-contained -o C:\drop\app
.\ServoraSetup.exe install --source C:\drop\app
```

## Manual installation from the zip

Use this only when the installer cannot be used. Run in an elevated PowerShell:

```powershell
$install = 'C:\Program Files\Servora'
$data    = 'C:\ProgramData\Servora'
Expand-Archive .\Servora-1.0.0-win-x64.zip -DestinationPath $install

# New-Service stores the quoted path verbatim (no command-line quoting pitfalls); sc.exe sets the rest.
New-Service -Name Servora -DisplayName Servora -BinaryPathName "`"$install\Servora.exe`"" -StartupType Automatic
sc.exe config Servora start= delayed-auto obj= "NT SERVICE\Servora"
sc.exe sidtype Servora unrestricted
sc.exe failure Servora reset= 86400 actions= restart/60000/restart/60000/restart/300000
sc.exe failureflag Servora 1

New-Item -ItemType Directory -Force $data | Out-Null
icacls $install /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F" "*S-1-5-32-545:(OI)(CI)RX"
icacls $data /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F" "NT SERVICE\Servora:(OI)(CI)M"

Start-Service Servora
Start-Process http://localhost:5080/setup
```

## Uninstall

```powershell
.\ServoraSetup.exe uninstall              # interactive: asks whether to delete the data (default: keep)
.\ServoraSetup.exe uninstall --keep-data  # keep database, keys, attachments, logs and backups
.\ServoraSetup.exe uninstall --silent     # removes everything including the data directory
```

External SQL Server / PostgreSQL databases are never dropped. A later `install` reuses a kept data directory,
including the database configuration and the encryption keys.
