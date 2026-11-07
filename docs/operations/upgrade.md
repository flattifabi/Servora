# Upgrade and rollback

Servora is updated in place by the same `ServoraSetup-<version>-win-x64.exe` that installs it. The data directory
(database settings, SQLite database, Data Protection keys, attachments, logs) and the instance
`<data dir>\appsettings.json` are **never modified** by an update – only the program files are replaced.

## Before you update

1. Read the release notes (breaking changes, required actions).
2. **Back up the database** – see [backup-restore.md](backup-restore.md). The automatic rollback restores the
   *program files*; it cannot undo database schema migrations that a new version applied at startup.
3. Plan a short maintenance window: the service is stopped while files are replaced (typically under a minute,
   plus startup time).

## Update

Interactive – run the new installer and choose **Update**:

```
  Servora             1.0.0 installed, service Running
  What would you like to do?
    1) Update to 1.1.0 (backup + automatic rollback)
    ...
```

Unattended:

```powershell
.\ServoraSetup-1.1.0-win-x64.exe update --silent
# or: install-or-update, whichever applies
.\ServoraSetup-1.1.0-win-x64.exe --silent
```

### What happens

| Step | Detail |
|------|--------|
| 1. Stage | The payload is extracted to a temporary folder and validated. Nothing on the machine changes if this fails. |
| 2. Version check | Downgrades are refused (use `--force` only together with a database restore). The same version is allowed as a *repair*. |
| 3. Stop | The service is stopped (timeout 90 s). If it does not stop, the update aborts without changes. |
| 4. Backup | The install directory is copied to `%ProgramData%\Servora\backups\<yyyyMMdd-HHmmss>-v<old version>`. |
| 5. Replace | The install directory is emptied (except an `App_Data` folder) and filled with the new files, so no stale files of the old version remain. |
| 6. Start | The service is started. |
| 7. Health check | `/health` must return HTTP 2xx within 120 s (`--health-timeout`). |
| 8. Prune | Only the newest 5 backups are kept (`--keep-backups <n>`). Folders in `backups` that do not follow the naming scheme are never deleted. |

The install directory is taken from the registered service, so an installation in a custom folder is updated where it
is. The health URL is derived from the instance settings (`Urls` or `Kestrel:Endpoints`, plain HTTP preferred,
wildcard hosts mapped to `localhost`); override it with `--health-url` for HTTPS-only setups.

## Automatic rollback

If replacing files, starting the service or the health check fails, the installer:

1. stops the service,
2. restores the program files from the backup made in step 4,
3. starts the previous version and checks it,
4. exits with code **7** ("rolled back").

The failed attempt leaves nothing behind except the backup folder and the transcript in
`%ProgramData%\Servora\logs\setup-<timestamp>.log`. Find the cause in the service logs and the Windows Event Log
(see [troubleshooting.md](troubleshooting.md#an-update-was-rolled-back)) before retrying.

> If the new version migrated the database schema before it failed, the restored old version may not work with the
> newer schema. In that case restore the database backup taken before the update as well.

## Manual rollback

Use this to return to an earlier version after a *successful* update, or if the automatic rollback itself failed
(exit code 1 with "Rollback failed").

```powershell
# Elevated PowerShell
Get-ChildItem C:\ProgramData\Servora\backups | Sort-Object Name   # pick the backup, e.g. 20261107-143000-v1.0.0
$backup  = 'C:\ProgramData\Servora\backups\20261107-143000-v1.0.0'
$install = 'C:\Program Files\Servora'

Stop-Service Servora
Get-ChildItem $install -Force | Where-Object Name -ne 'App_Data' | Remove-Item -Recurse -Force
Copy-Item "$backup\*" $install -Recurse -Force
# Restore the database backup from before the update if the newer version changed the schema.
Start-Service Servora
Invoke-WebRequest http://localhost:5080/health -UseBasicParsing | Select-Object StatusCode
```

Alternatively run the installer of the older release with `update --force` – it performs the same steps with backup and
health check.

## Upgrading from a manual (zip) installation

If Servora was installed manually as the service `Servora`, the installer updates it like any other installation. The
data directory is resolved exactly like the service does: service environment variable `SERVORA_DATA_DIR`, then the
machine environment variable, then `%ProgramData%\Servora`.

## Version scheme

Releases follow [Semantic Versioning](https://semver.org/). Tags `vX.Y.Z` create releases, tags like
`vX.Y.Z-beta.1` create pre-releases. `ServoraSetup status` shows the installed version and the version of the
installer's payload.
