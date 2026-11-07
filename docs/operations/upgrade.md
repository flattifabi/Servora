# Upgrade and rollback

Servora is updated in place by the same `ServoraSetup-<version>-win-x64.exe` that installs it. The installer never
modifies the data directory (database settings, SQLite database, Data Protection keys, attachments, logs) or the
instance `<data dir>\appsettings.json` – only the program files are replaced. The **database schema is migrated
automatically** when the new version starts, guarded by an automatic backup (see
[Automatic database migrations](#automatic-database-migrations)).

## Before you update

1. Read the release notes (breaking changes, required actions).
2. Make sure **Back up before database migrations** is enabled in **Backup & Restore** (default) and that the backup
   destination has free space for one more backup. Optionally click **Back up now** and copy the archive off the server.
3. Plan a short maintenance window: the service is stopped while files are replaced (typically under a minute,
   plus startup time and the pre-migration backup).

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
| 6. Start | The service is started. If the new version brings database migrations it first creates a pre-migration backup, then migrates (see below). |
| 7. Health check | `/health` must return HTTP 2xx within 120 s (`--health-timeout`). For large databases raise the timeout, because the pre-migration backup runs before Servora answers. |
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

Binaries and schema stay consistent: if a migration fails, the new version restores the pre-migration backup itself
before it exits with an error, so the previous version finds the database exactly as it left it. If the new version
migrated successfully but failed later (for example the health check timed out), restore the **Pre-migration** backup
from **Backup & Restore** or with `Servora.exe backup restore <file>` after the rollback (see
[Rolling back the database](#rolling-back-the-database)).

## Automatic database migrations

Every start checks the database schema and brings it up to date – no manual step after install or update:

| Situation | What Servora does |
|-----------|-------------------|
| Fresh, empty database | Creates the schema by applying all migrations. |
| Pending migrations on an existing database | 1. Creates a **Pre-migration** backup (kind *Pre-migration* in Backup & Restore; setting *Back up before database migrations*, default on). If the backup fails, nothing is changed and Servora stops with an error. 2. Applies the migrations. |
| A migration fails | Restores the pre-migration backup automatically, logs the failure (critical) and stops with a non-zero exit code. The installer's health check fails and rolls the program files back, so the previous version starts again with its original database. |
| The database contains migrations this version does not know | Stops: the database was upgraded by a newer version (a downgrade). Install that version again, or restore a backup made with this version. |
| Tables exist but there is no migration history | Stops with instructions – see [Legacy databases](#legacy-databases-without-migration-history). |

Log messages (Windows Event Log / `<data dir>\logs`) name the migrations, the backup file and the outcome. Pre-migration
backups are kept according to the retention setting *Keep safety backups* (default 5).

## Rolling back the database

To return to the state before an update (for example after a manual binary rollback to the previous version):

1. Find the *Pre-migration* backup created by the update in **Backup & Restore** (or `Servora.exe backup list`).
2. With the **old** version installed and the service stopped, run
   `Servora.exe backup restore <file> --data-dir C:\ProgramData\Servora`, then start the service.

A newer version can restore backups of older versions (it migrates them forward after the restore); an older version
refuses backups whose migration it does not know.

## Legacy databases without migration history

Pre-release builds created the schema without migrations (`EnsureCreated`). Such a database has Servora tables but no
`__EFMigrationsHistory` table, and a version with migrations refuses to start with an explanatory message instead of
guessing. Options:

- **Start fresh (recommended for evaluation installations):** stop the service, move `<data dir>\servora.db` away
  (SQLite) or point the connection to a new, empty database, and start Servora – it creates the schema with migrations.
- **Baseline an existing schema (advanced):** only if the schema exactly matches the first migration of the new
  version, create the history table and mark that migration as applied, then start Servora so it applies the later
  migrations:

  ```sql
  CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) NOT NULL PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
  INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('<first migration id>', '10.0.0');
  ```

  The migration identifiers are listed on the *Database schema* card of Backup & Restore and in the release notes.
  Take a database-native backup first.

Backups created by pre-release builds without migrations cannot be restored into versions with migrations.

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
# If the newer version migrated the schema, restore its pre-migration backup with the old version:
# & "$install\Servora.exe" backup restore <servora-…-premigration.svbak> --data-dir C:\ProgramData\Servora
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
