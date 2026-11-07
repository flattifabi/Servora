using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Servora.Tests.Unit.Backup.Migrations;

/// <summary>Test model with two hand-written migrations (<c>Initial</c>, <c>AddTag</c>).</summary>
/// <param name="options">The options.</param>
internal sealed class NoteLabContext(DbContextOptions<NoteLabContext> options) : DbContext(options)
{
    /// <summary>Creates a context for a SQLite connection string.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The context.</returns>
    public static NoteLabContext Create(string connectionString) =>
        new(new DbContextOptionsBuilder<NoteLabContext>()
            .UseSqlite(connectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) => NoteMigrationSteps.ConfigureCurrentModel(modelBuilder);
}
