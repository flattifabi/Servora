using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Servora.Tests.Unit.Backup.Migrations;

/// <summary>First migration of <see cref="FailingNoteLabContext"/>: creates the Notes table.</summary>
[DbContext(typeof(FailingNoteLabContext))]
[Migration(NoteMigrationSteps.Initial)]
internal sealed class FailingNoteInitialMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) => NoteMigrationSteps.CreateNotes(migrationBuilder);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) => NoteMigrationSteps.DropNotes(migrationBuilder);

    /// <inheritdoc />
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => NoteMigrationSteps.BuildModel(modelBuilder, withTag: false);
}
