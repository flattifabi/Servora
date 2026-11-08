# Database migrations

Servora supports SQLite, Microsoft SQL Server and PostgreSQL with one EF Core model (`ServoraDbContext` in
`Servora.Infrastructure`). The schema of every provider is managed with **EF Core migrations**, one set per provider:

| Provider | Project (migrations assembly) | Folder |
|----------|-------------------------------|--------|
| SQLite | `src/Servora.Persistence.Sqlite` | `Migrations/` |
| SQL Server | `src/Servora.Persistence.SqlServer` | `Migrations/` |
| PostgreSQL | `src/Servora.Persistence.PostgreSql` | `Migrations/` |

Each project sets itself as `MigrationsAssembly` in its `IDatabaseProvider.Configure` and contains a
`DesignTimeDbContextFactory` for the `dotnet ef` tooling, so no database server is needed to add a migration.
The schema is **never** created with `EnsureCreated` – every environment (development, tests of the startup path,
production) applies migrations.

## Prerequisites

- .NET SDK 10 and the EF Core tools: `dotnet tool install --global dotnet-ef` (a 10.0.x version; a patch version
  older than the EF runtime only prints a harmless warning).
- `Microsoft.EntityFrameworkCore.Design` is already referenced by the provider projects.
- Run `dotnet build Servora.slnx` (or `dotnet restore`) once after cloning – `dotnet ef` builds the project but does
  not restore packages and fails with `NETSDK1004` / "Unable to retrieve project metadata" otherwise.

## Adding a migration

Whenever an entity, an `IEntityTypeConfiguration<T>` or a convention changes, add a migration **for all three
providers with the same name**, from the repository root:

```powershell
$name = "AddIncidentTags"   # PascalCase, describes the change
dotnet ef migrations add $name --project src/Servora.Persistence.Sqlite     --startup-project src/Servora.Persistence.Sqlite     --output-dir Migrations
dotnet ef migrations add $name --project src/Servora.Persistence.SqlServer  --startup-project src/Servora.Persistence.SqlServer  --output-dir Migrations
dotnet ef migrations add $name --project src/Servora.Persistence.PostgreSql --startup-project src/Servora.Persistence.PostgreSql --output-dir Migrations
```

Each command creates `<timestamp>_<Name>.cs`, `<timestamp>_<Name>.Designer.cs` and updates
`ServoraDbContextModelSnapshot.cs`. Then:

1. **Review the generated `Up`/`Down` code** of every provider (renames are generated as drop + add – change them to
   `RenameColumn`/`RenameTable` by hand, add data conversions where needed). Keep migrations provider-neutral in
   intent; raw `migrationBuilder.Sql(...)` must be written per provider.
2. Run `dotnet build Servora.slnx` (0 warnings) and `dotnet test` – the pending-changes test (below) must pass for all
   three providers.
3. Commit all three migrations together with the model change.

Undo the last, **not yet committed/released** migration with
`dotnet ef migrations remove --project src/Servora.Persistence.<X> --startup-project src/Servora.Persistence.<X>`
(per provider; it also reverts the snapshot). `remove` checks whether the migration is applied to the design-time
database (`App_Data/design.db` for SQLite, or `SERVORA_DESIGN_CONNECTION`); for SQL Server / PostgreSQL without a
reachable server add `--force` (otherwise it fails after a connection timeout), or point `SERVORA_DESIGN_CONNECTION`
at a scratch database, for example
`Server=(localdb)\MSSQLLocalDB;Database=ServoraDesign;Trusted_Connection=True;TrustServerCertificate=True`.

### Rules

- **Names:** PascalCase verb phrase describing the change (`AddIncidentTags`, `RenameCiOwner`,
  `IndexAuditActorName`). Use the same name for all providers; the timestamps may differ.
- **Never edit or delete a migration that was released** (or merged to `main`) – installations have it in their
  `__EFMigrationsHistory`. Fix mistakes with a new migration.
- **Keep the `.Designer.cs` files** and the model snapshot. The backup module reads each migration's `TargetModel`
  to describe the schema a database is at (pre-migration backups, restoring an older backup).
- Migrations live only in the provider projects; tasks working on modules do not add migrations themselves
  (ADR-014) – the integration step generates them.
- Generated code is excluded from style analyzers by the `Migrations/.editorconfig` (`generated_code = true`); do not
  add other suppressions.

## The pending-changes test

`tests/Servora.Tests.Unit/Persistence/Migrations/` contains, without needing a database server:

| Test | What it guards |
|------|----------------|
| `ProviderMigrationTests.Model_HasNoPendingChanges_ComparedToTheMigrationSnapshot` | per provider, `Database.HasPendingModelChanges()` is false – **fails when the model changed but no migration was added** (for that provider). |
| `ProviderMigrationTests.Migrations_LiveInTheProviderProject_…` | migrations are in the provider assembly, start with `InitialCreate` and keep their designer file. |
| `ProviderMigrationTests.MigrationScript_CreatesEveryTableOfTheModel` | the generated SQL script of each provider creates every table. |
| `SqliteMigrationApplyTests` | all migrations apply to an empty SQLite database; every table and column of the model exists. |
| `StartupMigrationTests` | the real startup path (`DatabaseInitializer` → `DatabaseMigrationRunner` → seeders) on a fresh SQLite file, a second start, and the legacy-database stop. |

If the pending-changes test fails, add the missing migration with the commands above – do not change the test.

## How migrations are applied

Nobody runs `dotnet ef database update` against an installation: **Servora migrates itself at startup**
(`DatabaseInitializer` → `DatabaseMigrationRunner`, see [upgrade.md](../operations/upgrade.md#automatic-database-migrations)):

| Situation | Behaviour |
|-----------|-----------|
| Fresh, empty database | All migrations are applied, then the seeders run. |
| Pending migrations on an existing database | A **pre-migration backup** (`.svbak`, kind *PreMigration*) is created first (setting *Back up before database migrations*, default on), then the migrations are applied. |
| A migration fails | The pre-migration backup is **restored automatically**, startup stops with a critical log entry and a non-zero exit code; the installer's health check then rolls the program files back. |
| Database at a migration this version does not know | Startup stops (downgrade without restore). |
| Tables but no `__EFMigrationsHistory` | Legacy database (see below) – startup stops with instructions. |

The setup wizard creates the schema of a new database with the same migrations.

To try a migration locally, simply start the application (`dotnet run --project src/Servora.Web`) against a
development database; the pre-migration backup lands in `App_Data/backups/instance`. For SQL Server or PostgreSQL
you can also point the tooling at a scratch database:
`$env:SERVORA_DESIGN_CONNECTION = "<connection string>"; dotnet ef database update --project src/Servora.Persistence.SqlServer --startup-project src/Servora.Persistence.SqlServer`.

## Legacy databases (created before migrations)

Databases created by pre-release builds with `EnsureCreated` have Servora tables but no `__EFMigrationsHistory`.
Servora refuses to start on them instead of guessing which schema they have. Backups taken from such a database
cannot be restored into a version with migrations either.

- **Development / evaluation database:** reset it – stop Servora, delete `App_Data/servora.db` (SQLite; also the
  `-wal`/`-shm` files) or drop/recreate the SQL Server / PostgreSQL database, and start again. The schema is created
  by migrations and seeded (`admin` / `admin`).
- **Data worth keeping:** baseline the schema as described in
  [upgrade.md › Legacy databases](../operations/upgrade.md#legacy-databases-without-migration-history) (only when the
  schema matches `InitialCreate` exactly), after a database-native backup.
