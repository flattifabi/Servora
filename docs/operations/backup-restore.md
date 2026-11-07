# Backup and restore

A complete Servora backup consists of **the database** and **the data directory**. Program files are not part of a
backup – reinstall them with the installer of the same version.

## What to back up

| Item | Location | Why |
|------|----------|-----|
| Database | SQLite: `<data dir>\servora.db` (plus `-wal`/`-shm` files while running) · SQL Server / PostgreSQL: the database configured in the setup wizard | All records, configuration, users, audit log. |
| **Data Protection key ring** | `<data dir>\keys\*.xml` | Decrypts the database connection string in `database.json` and every secret in the secret store (SMTP/LDAP passwords, REST data source credentials, AI provider keys). **Without the keys these secrets are lost.** |
| Database settings | `<data dir>\database.json` | Provider and encrypted connection string. |
| Instance settings | `<data dir>\appsettings.json` | Port/URLs, TLS certificate configuration, logging overrides. |
| Attachments | `<data dir>\attachments\` | Files attached to records (the database only stores the metadata). |
| Logs (optional) | `<data dir>\logs\` | Diagnostics. |
| TLS certificate (if file based) | wherever `Kestrel` points to | Needed to serve HTTPS after a restore. |

`<data dir>` is `C:\ProgramData\Servora` unless `SERVORA_DATA_DIR` is set (`ServoraSetup status` shows it).
`<data dir>\backups` contains the installer's binary backups and does not need to be backed up.

### About the Data Protection keys (important)

The key ring is protected with **machine-level DPAPI** (ADR-007). Consequences:

- A backup of `keys\` restores secrets **on the same Windows installation** (same machine, also after reinstalling
  Servora or changing the service account).
- On a **different machine** (new hardware, cloned VM with a new identity, disaster recovery) the key files cannot be
  decrypted. Records, users and configuration in the database are unaffected, but Servora cannot read the stored
  connection string and secrets there: configure the database connection again (setup wizard or
  [pre-configured database](configuration.md#pre-configuring-the-database)) and re-enter the secrets (SMTP, LDAP,
  data source and AI credentials) in the administration UI.
- Keep the key ring and the database backup together and protect both like passwords – the key files are the
  encryption keys for the secrets inside the database.

## Consistent backups

Attachments and database must match. The simplest consistent backup stops the service for a moment:

```powershell
# Elevated PowerShell – full backup of a SQLite installation
$data   = 'C:\ProgramData\Servora'
$target = "E:\Backup\Servora\$(Get-Date -Format yyyyMMdd-HHmmss)"

Stop-Service Servora
try {
    New-Item -ItemType Directory -Force $target | Out-Null
    robocopy $data $target /E /COPY:DAT /XD "$data\backups" /R:2 /W:2 /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
}
finally {
    Start-Service Servora
}
```

Schedule it with Task Scheduler (run as SYSTEM, "Run with highest privileges") and copy the result off the server.
For SQL Server and PostgreSQL you can back up without downtime: take the database backup below **first**, then copy
the data directory. Attachments uploaded in between then only appear as unreferenced extra files – never as records
whose files are missing.

## SQLite

The SQLite database is a file in the data directory (connection string `Data Source={DataDirectory}/servora.db`).

- **Backup:** stop the service and copy `servora.db` (the copy procedure above does this). Copying the file of a running
  instance can produce a corrupt backup. If you have the `sqlite3` command-line tool, an online backup is possible:
  `sqlite3 C:\ProgramData\Servora\servora.db ".backup 'E:\Backup\servora.db'"`.
- **Restore:** stop the service, delete `servora.db-wal` and `servora.db-shm` if present, copy the backup to
  `servora.db`, start the service.

## Microsoft SQL Server

```sql
-- Backup (full, compressed, verified)
BACKUP DATABASE [Servora]
  TO DISK = N'E:\Backup\Servora_full.bak'
  WITH COMPRESSION, CHECKSUM, INIT, NAME = N'Servora full backup';
RESTORE VERIFYONLY FROM DISK = N'E:\Backup\Servora_full.bak' WITH CHECKSUM;

-- Restore (stop the Servora service first)
ALTER DATABASE [Servora] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [Servora] FROM DISK = N'E:\Backup\Servora_full.bak' WITH REPLACE, CHECKSUM;
ALTER DATABASE [Servora] SET MULTI_USER;
```

Use the full recovery model with log backups if you need point-in-time recovery. When restoring to another SQL Server,
re-create the login of the service identity (the computer account `DOMAIN\SERVER$` for the default virtual service
account, or the SQL login from the connection string) and map it to the database user.

## PostgreSQL

```powershell
# Backup (custom format, compressed)
pg_dump --host=db01 --username=servora --format=custom --file=E:\Backup\servora.dump servora

# Restore into an existing, empty or to-be-replaced database (stop the Servora service first)
pg_restore --host=db01 --username=servora --dbname=servora --clean --if-exists --no-owner E:\Backup\servora.dump
```

Store the password in `%APPDATA%\postgresql\pgpass.conf` of the account running the backup job instead of on the
command line. For continuous protection use PostgreSQL's WAL archiving / `pg_basebackup`.

## Restore procedure

1. Install Servora **of the same version** that made the backup (`ServoraSetup install`, or keep the existing
   installation). Do not start the setup wizard.
2. Stop the service: `Stop-Service Servora`.
3. Restore the data directory content (`database.json`, `keys\`, `attachments\`, `appsettings.json`) into
   `<data dir>`, then re-apply the permissions:

   ```powershell
   icacls C:\ProgramData\Servora /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F" "NT SERVICE\Servora:(OI)(CI)M" /T
   ```
4. Restore the database (provider sections above).
5. Start the service and check `ServoraSetup status` and the sign-in.
6. On a different machine: configure the database connection again and re-enter the secrets (see the key ring section).

Then update to the current version if needed – see [upgrade.md](upgrade.md).

## Test your backups

Restore into a separate test machine regularly (for example quarterly) and verify sign-in, a few records with
attachments, and an outgoing e-mail (which proves the secrets are readable or have been re-entered).
