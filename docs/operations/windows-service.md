# Windows service

Servora's host (`Servora.exe`) uses `UseWindowsService`: started by the Service Control Manager it runs as a service,
started from a console it runs as a normal console application (development).

| Property | Value |
|----------|-------|
| Service name / display name | `Servora` / `Servora` |
| Executable | `<install dir>\Servora.exe` (registered with quotes) – default `C:\Program Files\Servora` |
| Startup type | Automatic (delayed start) – starts shortly after boot, after core services such as networking and SQL Server |
| Account | `NT SERVICE\Servora` (virtual service account) by default |
| Recovery | Restart after 1 min, 1 min, 5 min; failure counter reset after 24 h; also applied when the process exits with an error |
| Service SID | `unrestricted` (required for the virtual account and for granting it file or certificate permissions) |
| Content root | the install directory (`AppContext.BaseDirectory`) |
| Data directory | `%ProgramData%\Servora` (override: service environment variable `SERVORA_DATA_DIR`) |
| Environment | `Production` (no `ASPNETCORE_ENVIRONMENT` set) |
| Event log | Application log, source `Servora` (warnings and errors by default) |

## Everyday commands

```powershell
Get-Service Servora                    # state
Restart-Service Servora                # restart (e.g. after changing listen URLs or certificates)
Stop-Service Servora; Start-Service Servora
sc.exe qc Servora                      # registered configuration
sc.exe qfailure Servora                # recovery actions
.\ServoraSetup.exe status              # state, versions, URLs, health, backups (no admin rights needed)
Invoke-WebRequest http://localhost:5080/health -UseBasicParsing
```

Recent service events:

```powershell
Get-WinEvent -FilterHashtable @{ LogName = 'Application'; ProviderName = 'Servora' } -MaxEvents 20 |
    Format-Table TimeCreated, LevelDisplayName, Message -Wrap
```

Application logs (with the debug switch from the UI) are written to `<data dir>\logs`.

## Service account

| Account | When to use | Notes |
|---------|-------------|-------|
| `NT SERVICE\Servora` (default) | Recommended for all installations. | No password, minimal local rights. On the network it authenticates as the **computer account** (`DOMAIN\SERVER$`) – Windows authentication to SQL Server, LDAP binds with the default credentials and Kerberos for Windows SSO work like with LocalSystem. |
| `LocalSystem` | Only if a feature needs full local system rights. | Highest privilege; avoid. |
| `NetworkService` | Legacy policies that require a built-in account. | Shared by many services. |
| Domain account / gMSA | Policies requiring a dedicated domain identity (e.g. SQL Server login per service). | Configure after installation (below). |

Choose a built-in account during installation with `--service-account virtual|localsystem|networkservice`.

### Changing to a domain account or gMSA

1. Grant the account *Log on as a service* (done automatically by `services.msc`).
2. Grant permissions on the data directory (the installer restricted it to SYSTEM, Administrators and the original
   service account):

   ```powershell
   icacls C:\ProgramData\Servora /grant "CONTOSO\svc-servora:(OI)(CI)M"
   ```
3. Set the account: `services.msc` → Servora → *Log On*, or for a gMSA
   `sc.exe config Servora obj= "CONTOSO\gmsa-servora$"` (no password).
4. If HTTPS uses a certificate from the Windows store, grant the account read access to the private key
   (see [configuration.md](configuration.md#https-with-kestrel)).
5. Restart the service.

> **Data Protection keys** are protected with machine-level DPAPI (ADR-007), so changing the service account does not
> make existing secrets unreadable – moving to another machine does (see [backup-restore.md](backup-restore.md)).

## Service environment variables

Environment variables for the service only are stored in the registry value
`HKLM\SYSTEM\CurrentControlSet\Services\Servora\Environment` (REG_MULTI_SZ, one `NAME=value` per line). The installer
uses it for `SERVORA_DATA_DIR` when you pass `--data-dir`. To add a variable without losing existing ones:

```powershell
$key = 'HKLM:\SYSTEM\CurrentControlSet\Services\Servora'
$current = @((Get-ItemProperty $key -Name Environment -ErrorAction SilentlyContinue).Environment) | Where-Object { $_ }
$updated = @($current | Where-Object { $_ -notlike 'ASPNETCORE_FORWARDEDHEADERS_ENABLED=*' }) + 'ASPNETCORE_FORWARDEDHEADERS_ENABLED=true'
Set-ItemProperty $key -Name Environment -Type MultiString -Value $updated
Restart-Service Servora
```

Prefer the instance `appsettings.json` for application settings – it is easier to back up and review (see
[configuration.md](configuration.md)).

## Ports and URLs

The listen URLs come from `Urls` (default `http://0.0.0.0:5080`) or `Kestrel:Endpoints` in the instance settings.
Kestrel listens directly; no `netsh http add urlacl` reservation is needed, also not for ports below 1024. Change the
port with the installer on a fresh installation (`--port`) or afterwards in `<data dir>\appsettings.json`, then restart
the service and adjust the firewall rule:

```powershell
Set-NetFirewallRule -DisplayName Servora -LocalPort 8443
```

## Running several instances

One machine hosts one Servora instance: the service name `Servora` is fixed. Use separate machines or VMs for
test/staging and production.

## Running from the console (diagnostics)

Stop the service and run the host in a console as an administrator to see startup errors directly:

```powershell
Stop-Service Servora
Set-Location 'C:\Program Files\Servora'            # console runs use the current directory as content root
$env:SERVORA_DATA_DIR = 'C:\ProgramData\Servora'   # use the service's data directory instead of .\App_Data
.\Servora.exe
```

Press `Ctrl+C` to stop, then `Start-Service Servora`. New files inherit the data directory permissions, so the service
account can still use them.
