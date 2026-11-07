# Backup & Restore module (T-15)

Servora backs itself up – database, Data Protection key ring and attachments – into a single, verifiable archive,
on a schedule, on demand, and automatically before every database migration and every restore. Any backup can be
restored ("rolled back") from the UI at `/admin/backups` or from the command line. Design: [ADR-015](../../agent/DECISIONS.md).

Operator guide: [operations/backup-restore.md](../operations/backup-restore.md) · upgrades and automatic migrations:
[operations/upgrade.md](../operations/upgrade.md).

## Features

| Feature | Where |
|---------|-------|
| KPIs (last successful backup, next run, count, total size), alerts for failures / last restore / disabled schedule | `/admin/backups` |
| Backup list (kind, date, version, migration, size, integrity, encryption) with search, filter, sort, paging | `/admin/backups` |
| Back up now, upload (validated before it is listed), download, verify (SHA-256 + signature), delete | row actions / header |
| Restore with typed `RESTORE` confirmation, pre-restore safety backup, restart and "restoring" screen | restore dialog → `/admin/backups/restoring` |
| Schedule editor (daily / weekly / monthly, weekdays, day of month, time, time zone, destination incl. UNC, attachments, encryption, retention, catch-up, pre-migration backups) with live "next runs" preview | drawer |
| Database schema card (provider, current / applied / pending migrations, problems) | sidebar |
| Disaster-recovery command line | `Servora backup list|create|verify|restore` |
| Automatic pre-migration backup and automatic restore after a failed migration | startup (`DatabaseInitializer`) |

## Permissions

| Key | Grants |
|-----|--------|
| `backup.read` | View the page, the list, the schedule and the schema state; verify archives. |
| `backup.manage` | Back up now, upload, download, delete, change the schedule and the encryption password. |
| `backup.restore` | Restore (roll back) the instance. |

Only the built-in `admin` role (`*`) holds them by default. Every service method checks the permission server-side;
the page carries `[RequirePermission(backup.read)]`, the download endpoint requires `backup.manage`. All actions are
audited (category `backup`: `backup.created`, `backup.failed`, `backup.verified`, `backup.deleted`, `backup.uploaded`,
`backup.downloaded`, `backup.schedule.updated`, `backup.restore.requested`, `backup.restored`, `backup.restore.failed`).
Operations that run before the audit log is usable (restore at startup, pre-migration backup, command line) are queued
in `backup.json` and written by the scheduler once Servora runs.

## Archive format (`.svbak`)

A zip file:

| Entry | Content |
|-------|---------|
| `manifest.json` | Format version, app version, provider, last applied migration, created at/by, machine, kind, and the schema of every table (columns with store type / nullability / parameter type, primary key, foreign keys), row counts and the SHA-256 and length of every other entry. |
| `manifest.sig` | Encrypted archives only: HMAC-SHA256 of the manifest bytes. |
| `db/NNNN.bin` | One logical dump per table: rows of typed values (null, string, integers, floating point, decimal, bool, bytes, GUID, `DateTime` incl. kind, `DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly`). Streamed – one row in memory at a time. |
| `keys/…` | The Data Protection key ring (`<data dir>\keys`). |
| `attachments/…` | Attachment binaries (optional). |
| `data/branding/…`, `data/uploads/…` | Other data directory content, when present. |
| `data/appsettings.json` | The instance settings – only when "Include appsettings.json" is enabled. |

`database.json` (encrypted connection string) and `backup.json` are **never** part of an archive.

**Schema source.** Table metadata comes from the EF Core relational model of the migration the database is *at*
(`Migration.TargetModel` of the last applied migration), not from the model of the running version. A pre-migration
backup therefore describes the old schema exactly, and a restore is self-contained.

**Integrity.** Entry hashes are computed over the stored entry bytes (after encryption), so tampering and corruption
are detected without the password; the manifest is validated structurally (paths, identifiers, store types) because
archives can be uploaded. Encrypted archives additionally authenticate every chunk (AES-GCM) and the manifest
(HMAC), so modifications are detected even by someone who recomputes the hashes.

**Encryption.** Optional, with the backup password stored in the secret store (`backup.encryption_password`):
PBKDF2-SHA256 (600 000 iterations, random salt per archive) derives an AES-256-GCM key and an HMAC key. Entries are
deflated, then encrypted in 64 KiB chunks; the nonce is entry index + chunk counter and the additional data binds the
entry path, chunk index and "final chunk" flag (no reordering, moving or truncation). The password is never logged,
never returned to the UI and never written in clear text (restore requests keep it protected with Data Protection).

## Backup

`BackupEngine.CreateAsync` reads all tables inside one read transaction – a consistent snapshot on SQLite (deferred
transaction, WAL), PostgreSQL (repeatable read) and SQL Server when `ALLOW_SNAPSHOT_ISOLATION` is on (otherwise read
committed; enable snapshot isolation for consistent online backups) – then adds key ring, attachments and data files,
writes the manifest last and renames `*.partial` to the final name. Names: `servora-yyyyMMdd-HHmmss-<kind>.svbak`.

Kinds: `Scheduled`, `Manual`, `PreMigration`, `PreRestore`, `Uploaded` (uploads keep the original kind in the manifest).

## Restore

1. UI / CLI verifies the archive (hashes, password, signature) and compatibility: same provider only; the backup's
   migration must be known to this version (backups of newer versions, and backups without migration history into a
   version with migrations, are refused).
2. A **pre-restore safety backup** is created, the request is written to `<data dir>\restore-request.json`, and
   Servora stops with exit code **75**. As a Windows service the exit code is reported to the service control manager,
   whose recovery actions (`sc failureflag Servora 1`, set by the installer) start Servora again after one minute.
   In a console (development) start it again yourself. The browser shows a "restoring" page that polls `/health`.
3. At startup, **before migrations and before any background service**, `PendingRestoreProcessor`:
   migrates the database to the backup's migration (`IMigrator.MigrateAsync(target)`, runs `Down` migrations when the
   database is newer; `EnsureCreated` for builds without migrations), then in **one transaction** sets deferred foreign
   key columns to null, deletes all rows in reverse foreign key order, inserts the rows in foreign key order (nullable
   self references and cycle-closing foreign keys are written in a second `UPDATE` pass – no superuser rights or
   disabled constraints needed) and handles identity columns (SQL Server `IDENTITY_INSERT`, PostgreSQL
   `OVERRIDING SYSTEM VALUE` + sequence reset). Then it restores attachments and data files and **merges** the key ring
   (existing keys are kept so `database.json` stays readable).
4. The normal `DatabaseInitializer` migrates the restored database forward to the current version.

If the restore fails after it started to change the database, the safety backup is restored automatically and startup
continues (the failure is shown on the page and audited). A request that is interrupted twice (crash, power loss) is
parked as `restore-request.json.failed` so a broken restore can never cause a restart loop.

Raw SQL exists only in `Servora.Infrastructure/Backup/**`: identifiers are delimited with `ISqlGenerationHelper`,
values are always parameters, and the only interpolated non-identifier text (PostgreSQL `CAST(@p AS <store type>)`)
is validated against a strict pattern.

## Schedule, retention and state (`backup.json`)

Schedule, retention and runtime state live in `<data dir>\backup.json`, not in the database, so they survive restores.

- The hosted `BackupSchedulerService` re-evaluates at least once per minute (`TimeProvider`): an occurrence is run
  once; one missed during downtime is run as soon as Servora is up again when **catch-up** is on (only the latest
  missed one), otherwise skipped. Saving the schedule resets the baseline, so no immediate catch-up is triggered.
- Daylight saving time: a run time that does not exist (spring forward) moves forward by the gap; a run time that
  occurs twice (fall back) runs once, at its first occurrence. Monthly day 29–31 runs on the last day of shorter months.
- Retention after each backup: keep the newest *N* scheduled backups and drop scheduled backups older than *max age*
  (the newest scheduled backup is always kept); keep the newest *N* pre-migration and *N* pre-restore backups; manual
  and uploaded backups are never deleted automatically. 0 means unlimited.
- One backup or restore at a time: in-process semaphore plus an exclusive lock file (`backups\.backup.lock`), so the
  command line and the service never run concurrently.
- Failures are logged, audited, stored in `backup.json` and shown on the page.

Default destination: `<data dir>\backups\instance` (the installer's binary backups use `<data dir>\backups\<timestamp>`
and never touch this folder). UNC paths are allowed; the service account needs write access. Pre-migration and
pre-restore backups fall back to the default folder when the destination is unreachable.

## Automatic migrations (`DatabaseMigrationRunner`)

| Situation at startup | Behaviour |
|----------------------|-----------|
| Build without migrations | `EnsureCreated` (development before T-13). |
| Fresh, empty database | Apply all migrations. |
| Pending migrations on an existing database | `PreMigration` backup (configurable, default on), then migrate. A failed backup stops startup before anything is changed. |
| A migration fails | Restore the pre-migration backup automatically, log critical, stop with a non-zero exit code (the installer's health check then rolls back the program files). |
| Tables but no `__EFMigrationsHistory` (legacy `EnsureCreated` database) | Stop with instructions ([upgrade.md](../operations/upgrade.md#legacy-databases-without-migration-history)). |
| Database contains migrations unknown to this version | Stop: the database was upgraded by a newer version. |

## Command line

```text
Servora backup list
Servora backup create  [--no-attachments] [--password <pw>]
Servora backup verify  <file> [--password <pw>]
Servora backup restore <file> [--password <pw>] [--no-safety-backup] [--yes]
  --data-dir <path>   data directory (default: SERVORA_DATA_DIR, then %ProgramData%\Servora when it holds
                      database.json and the working directory has no App_Data, otherwise ./App_Data)
```

`<file>` is a name from `backup list` or a path. The password can also come from `SERVORA_BACKUP_PASSWORD`; by default
the configured password is used. `restore` runs immediately in the command-line process – **stop the service first**.
It creates a safety backup (skip with `--no-safety-backup` when the current database is broken) and asks for `RESTORE`
unless `--yes` is given. Exit codes: 0 success, 1 failure, 2 usage.

## Configuration

| Setting | Default | Meaning |
|---------|---------|---------|
| `Servora:Backup:MaxUploadSizeMb` | 4096 | Upload limit of the backup page. |

## Code map

| Layer | Path |
|-------|------|
| Domain | `src/Servora.Domain/Backup/` – `BackupSchedule`, `BackupScheduleCalculator`, `BackupSchedulePlanner`, `BackupRetentionPlanner`, enums |
| Application | `src/Servora.Application/Backup/` – `IBackupService`, DTOs, `IApplicationRestarter`, `BackupConstants`; `Security/Permissions.Backup.cs` |
| Infrastructure | `src/Servora.Infrastructure/Backup/` – engine, archive reader/writer, cipher, dump/restore + dialects, catalog, settings, scheduler, restore processor, migration runner, CLI, `BackupService`; `Modules/BackupModule.cs`; `Persistence/DatabaseInitializer.cs` |
| Web | `Components/Pages/Admin/Backup/Backups.razor`, `Components/Backup/*`, `Endpoints/BackupEndpoints.cs`, `Modules/BackupWebModule.cs`, `Backup/WindowsServiceApplicationRestarter.cs` |
| Tests | `tests/Servora.Tests.Unit/Backup/` |

## Known limitations

- Restores only into the same provider (no SQLite → SQL Server migration through backups).
- Backups taken by builds without migrations cannot be restored into versions with migrations.
- On another machine the key ring is DPAPI-protected and cannot be decrypted – configure the database connection
  again and re-enter secrets (see the operations guide). The backup password is stored in the database and therefore
  reverts with a restore; the restore dialog accepts a different password for an archive.
- The upload goes through the Blazor circuit (`InputFile`), which is slower than a plain HTTP upload for multi-GB files.
