using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Servora.Tests.Unit.Backup.Migrations;

/// <summary>Test model with three migrations; the third one always fails (simulates a broken upgrade).</summary>
/// <param name="options">The options.</param>
internal sealed class FailingNoteLabContext(DbContextOptions<FailingNoteLabContext> options) : DbContext(options)
{
    /// <summary>Creates a context for a SQLite connection string.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The context.</returns>
    public static FailingNoteLabContext Create(string connectionString) =>
        new(new DbContextOptionsBuilder<FailingNoteLabContext>()
            .UseSqlite(connectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) => NoteMigrationSteps.ConfigureCurrentModel(modelBuilder);
}
