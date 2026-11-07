using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Servora.Tests.Unit.Backup.Migrations;

/// <summary>Hand-written migration operations and target models shared by the test migration contexts.</summary>
internal static class NoteMigrationSteps
{
    /// <summary>The identifier of the first migration.</summary>
    public const string Initial = "20260101000000_Initial";

    /// <summary>The identifier of the second migration.</summary>
    public const string AddTag = "20260201000000_AddTag";

    /// <summary>The identifier of the failing third migration.</summary>
    public const string Broken = "20260301000000_Broken";

    private const string EntityName = "Servora.Tests.Unit.Backup.Migrations.LabNote";

    /// <summary>Creates the Notes table.</summary>
    /// <param name="builder">The migration builder.</param>
    public static void CreateNotes(MigrationBuilder builder)
    {
        builder.CreateTable(
            name: "Notes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notes", x => x.Id);
                table.ForeignKey("FK_Notes_Notes_ParentId", x => x.ParentId, "Notes", "Id");
            });
        builder.CreateIndex("IX_Notes_ParentId", "Notes", "ParentId");
    }

    /// <summary>Drops the Notes table.</summary>
    /// <param name="builder">The migration builder.</param>
    public static void DropNotes(MigrationBuilder builder) => builder.DropTable("Notes");

    /// <summary>Adds the Tag column.</summary>
    /// <param name="builder">The migration builder.</param>
    public static void AddTagColumn(MigrationBuilder builder) =>
        builder.AddColumn<string>(name: "Tag", table: "Notes", type: "TEXT", maxLength: 50, nullable: true);

    /// <summary>Drops the Tag column.</summary>
    /// <param name="builder">The migration builder.</param>
    public static void DropTagColumn(MigrationBuilder builder) => builder.DropColumn(name: "Tag", table: "Notes");

    /// <summary>Builds the target model of a migration (as a designer file would).</summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="withTag">Whether the Tag column exists.</param>
    public static void BuildModel(ModelBuilder modelBuilder, bool withTag)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.10");
        modelBuilder.Entity(EntityName, b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("TEXT");
            b.Property<Guid?>("ParentId").HasColumnType("TEXT");
            if (withTag)
            {
                b.Property<string>("Tag").HasMaxLength(50).HasColumnType("TEXT");
            }

            b.Property<string>("Title").IsRequired().HasMaxLength(200).HasColumnType("TEXT");
            b.HasKey("Id");
            b.HasIndex("ParentId");
            b.ToTable("Notes");
        });
        modelBuilder.Entity(EntityName, b => b.HasOne(EntityName, null).WithMany().HasForeignKey("ParentId"));
    }

    /// <summary>Configures the current model (equal to the model after <see cref="AddTag"/>).</summary>
    /// <param name="modelBuilder">The model builder.</param>
    public static void ConfigureCurrentModel(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<LabNote>(b =>
        {
            b.ToTable("Notes");
            b.Property(n => n.Title).HasMaxLength(200).IsRequired();
            b.Property(n => n.Tag).HasMaxLength(50);
            b.HasOne<LabNote>().WithMany().HasForeignKey(n => n.ParentId);
        });
}
