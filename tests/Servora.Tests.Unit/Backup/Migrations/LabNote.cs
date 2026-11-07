namespace Servora.Tests.Unit.Backup.Migrations;

/// <summary>Test entity of the migration models: a note with a nullable self reference and a column added by the second migration.</summary>
internal sealed class LabNote
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the parent note (nullable self reference).</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Gets or sets the tag (added by migration <c>AddTag</c>).</summary>
    public string? Tag { get; set; }
}
