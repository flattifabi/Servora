using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Servora.Tests.Unit.Backup.Migrations;

/// <summary>First migration of <see cref="NoteLabContext"/>: creates the Notes table.</summary>
[DbContext(typeof(NoteLabContext))]
[Migration(NoteMigrationSteps.Initial)]
internal sealed class NoteInitialMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) => NoteMigrationSteps.CreateNotes(migrationBuilder);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) => NoteMigrationSteps.DropNotes(migrationBuilder);

    /// <inheritdoc />
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => NoteMigrationSteps.BuildModel(modelBuilder, withTag: false);
}
