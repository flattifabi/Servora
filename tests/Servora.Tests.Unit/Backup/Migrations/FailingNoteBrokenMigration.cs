using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Servora.Tests.Unit.Backup.Migrations;

/// <summary>Third migration of <see cref="FailingNoteLabContext"/>: always fails.</summary>
[DbContext(typeof(FailingNoteLabContext))]
[Migration(NoteMigrationSteps.Broken)]
internal sealed class FailingNoteBrokenMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("INSERT INTO \"MissingTable\" (\"Id\") VALUES (1);");

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }

    /// <inheritdoc />
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => NoteMigrationSteps.BuildModel(modelBuilder, withTag: true);
}
