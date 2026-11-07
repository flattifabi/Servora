namespace Servora.Infrastructure.Backup;

/// <summary>Schema and payload reference of one table in a backup archive.</summary>
public sealed class BackupTableManifest
{
    /// <summary>Gets or sets the table name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the schema, or <see langword="null"/> for the default schema.</summary>
    public string? Schema { get; set; }

    /// <summary>Gets or sets the archive entry holding the table dump.</summary>
    public string Entry { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of rows.</summary>
    public long RowCount { get; set; }

    /// <summary>Gets or sets the dumped columns in dump order.</summary>
    public List<BackupColumnManifest> Columns { get; set; } = [];

    /// <summary>Gets or sets the primary key column names.</summary>
    public List<string> PrimaryKey { get; set; } = [];

    /// <summary>Gets or sets the foreign keys.</summary>
    public List<BackupForeignKeyManifest> ForeignKeys { get; set; } = [];

    /// <summary>Gets a stable key (<c>schema.name</c>) used to match tables.</summary>
    public string Key => TableKey(Schema, Name);

    /// <summary>Builds the key of a table.</summary>
    /// <param name="schema">The schema.</param>
    /// <param name="name">The table name.</param>
    /// <returns>The key.</returns>
    public static string TableKey(string? schema, string name) => string.IsNullOrEmpty(schema) ? name : $"{schema}.{name}";

    /// <inheritdoc />
    public override string ToString() => Key;
}
