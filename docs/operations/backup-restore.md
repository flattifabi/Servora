# Backup and restore

Servora has a **built-in backup**: scheduled and manual backups of the database, the Data Protection key ring and the
attachments into one verifiable archive, automatic safety backups before every database migration and every restore,
retention, download/upload, and restore ("roll back") from the UI or the command line – on SQLite, SQL Server and
PostgreSQL, without external tools. Technical details: [modules/backup.md](../modules/backup.md).

Program files are not part of a backup – reinstall them with the installer (see [upgrade.md](upgrade.md)).

## Quick start

1. Sign in as an administrator and open **Operations › Backup & Restore** (`/admin/backups`).
2. Click **Back up now** to create the first backup.
3. Click **Edit schedule & retention** and set up the schedule, for example *Weekly on Sunday at 02:00*:
   - **Destination** – default `<data dir>\backups\instance`. Prefer a different disk or a UNC share
     (`\\backup01\servora`) so a disk failure does not take the backups with it. Grant the service account
     (`NT SERVICE\Servora`, on a share the computer account `DOMAIN\SERVER$`) modify rights.
   - **Encryption** – enable it and set a password (≥ 12 characters) whenever backups leave the server. Store the
     password in your password manager: encrypted backups cannot be restored without it.
   - **Retention** – e.g. keep 8 scheduled backups, max. 90 days, 5 safety backups per kind.
   - **Catch up a missed run** – runs a backup missed while the server was off as soon as Servora is up again.
   - **Back up before database migrations** – keep it on (default).
4. Copy the archives off the server regularly (or point the destination at a share that is itself backed up).

## What a backup contains

| Item | In the archive | Notes |
|------|----------------|-------|
| Database (all tables) | ✅ | Logical, provider-neutral dump with schema metadata and SHA-256 per entry. |
| Data Protection key ring `<data dir>\keys` | ✅ | Needed to decrypt secrets in the database. |
| Attachments `<data dir>\attachments` | optional (default on) | |
| Branding / uploads in the data directory | ✅ when present | |
| Instance `appsettings.json` | optional (default off) | On restore it is written as `appsettings.from-backup.json` and never overwrites the live file. |
| `database.json` (encrypted connection string) | ❌ never | |
| `backup.json` (schedule, state) | ❌ never | Survives restores. |
| Logs | ❌ | |

The archive contains password hashes and the key ring – **protect backups like passwords** (encryption, access rights).

### About the Data Protection keys (important)

The key ring is protected with **machine-level DPAPI** (ADR-007). Consequences:

- A restore on the **same Windows installation** restores all secrets. Restores *merge* the key ring: current keys are
  never removed, so the connection string in `database.json` stays readable.
- On a **different machine** (new hardware, cloned VM with a new identity, disaster recovery) the key files cannot be
  decrypted. Records, users and configuration are unaffected, but Servora cannot read stored secrets there: configure
  the database connection again (setup wizard or [pre-configured database](configuration.md#pre-configuring-the-database))
  and re-enter the secrets (SMTP, LDAP, data source and AI credentials) in the administration UI.

## Restore (roll back) from the UI

1. In the backup list click **Restore** (history icon) on the backup.
2. Check the summary, enter the archive password if it differs from the configured one, and type `RESTORE`.
3. Servora creates a **pre-restore safety backup** of the current state, then restarts:
   - **Windows service:** the service stops with exit code 75 and the service recovery actions (configured by the
     installer: restart after 1 minute, also on error exit codes) start it again. Expect about one to two minutes.
   - **Console / development:** Servora stops; start it again yourself.
4. At startup – before migrations – the database is set to the backup's schema version, all rows are replaced,
   attachments and keys are restored, then the database is migrated to the current version. The browser shows a
   *Restoring* page and returns to Backup & Restore when `/health` answers again. All users must sign in again.
5. The result is shown on the page (and in the audit log). If the restore failed, Servora rolled back to the safety
   backup automatically; to undo a successful restore, restore the **Pre-restore** backup.

Rules: only backups of the **same database provider**, created by **this or an older Servora version**, can be
restored. Backups of a newer version are refused – install that version first.

## Disaster recovery from the command line

Use the command line when the UI does not start (run it from an elevated prompt in the installation folder,
`C:\Program Files\Servora`):

```powershell
Stop-Service Servora
.\Servora.exe backup list    --data-dir C:\ProgramData\Servora
.\Servora.exe backup verify  servora-20261108-020000-scheduled.svbak --data-dir C:\ProgramData\Servora
.\Servora.exe backup restore servora-20261108-020000-scheduled.svbak --data-dir C:\ProgramData\Servora
Start-Service Servora
```

- `--data-dir` can be omitted when `SERVORA_DATA_DIR` is set or the service data directory
  `%ProgramData%\Servora` contains `database.json`.
- `<file>` is a name from `backup list` or a path to any `.svbak` file (for example copied back from a share).
- Encrypted archives: the configured password is used; otherwise pass `--password <pw>` or set
  `SERVORA_BACKUP_PASSWORD` (preferred – keeps the password out of the process list and shell history).
- `restore` asks you to type `RESTORE` (`--yes` skips it) and creates a safety backup first; use
  `--no-safety-backup` when the current database is broken.
- `backup create` creates a manual backup (`--no-attachments` to skip attachments).
- Exit codes: 0 success, 1 failure, 2 invalid arguments.

### Restore onto a new server

1. Install Servora of the **same or a newer version** (`ServoraSetup install`).
2. Configure the database connection (setup wizard *database step only*, or pre-configure `Servora:Database`), pointing
   to an empty database, and start Servora once so it creates the schema. Then stop the service.
3. Copy the backup file to the server and run `Servora.exe backup restore <path> --no-safety-backup`.
4. Start the service. Re-enter secrets (SMTP, LDAP, data sources, AI keys) – the key ring of the old machine cannot be
   decrypted here.

## Test your backups

- **Verify** (shield icon) checks the SHA-256 hash of every entry and, for encrypted archives with a known password,
  the manifest signature. Run it after copying archives around.
- Restore into a separate test installation regularly (for example quarterly) and check sign-in, a few records with
  attachments, and an outgoing e-mail.

## Alternative: external backups

The built-in backup is the recommended method. If your organisation requires database-native backups, you can use
them in addition:

| Provider | Backup | Restore (service stopped) |
|----------|--------|---------------------------|
| SQLite | Stop the service and copy `<data dir>\servora.db` (or `sqlite3 servora.db ".backup 'E:\Backup\servora.db'"` online). | Delete `servora.db-wal`/`-shm`, copy the file back. |
| SQL Server | `BACKUP DATABASE [Servora] TO DISK = N'E:\Backup\Servora_full.bak' WITH COMPRESSION, CHECKSUM, INIT;` | `RESTORE DATABASE [Servora] FROM DISK = N'…' WITH REPLACE, CHECKSUM;` (re-create the login of the service identity on another server). |
| PostgreSQL | `pg_dump --format=custom --file=E:\Backup\servora.dump servora` | `pg_restore --dbname=servora --clean --if-exists --no-owner E:\Backup\servora.dump` |

Always back up the data directory (`keys\`, `attachments\`, `database.json`, `appsettings.json`) together with a
database-native backup – take the database backup **first**, then copy the data directory. After restoring the data
directory re-apply the permissions:

```powershell
icacls C:\ProgramData\Servora /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F" "NT SERVICE\Servora:(OI)(CI)M" /T
```

`<data dir>\backups` holds the installer's binary backups (`<timestamp>-v<version>`) and the built-in backups
(`instance\`); exclude it from file-level copies of the data directory unless it is your only backup location.
