using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Servora.Tests.Unit.Backup.Migrations;

/// <summary>Second migration of <see cref="FailingNoteLabContext"/>: adds the Tag column (succeeds).</summary>
[DbContext(typeof(FailingNoteLabContext))]
[Migration(NoteMigrationSteps.AddTag)]
internal sealed class FailingNoteAddTagMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) => NoteMigrationSteps.AddTagColumn(migrationBuilder);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) => NoteMigrationSteps.DropTagColumn(migrationBuilder);

    /// <inheritdoc />
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => NoteMigrationSteps.BuildModel(modelBuilder, withTag: true);
}
