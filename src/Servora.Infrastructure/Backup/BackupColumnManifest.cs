using System.Data;

namespace Servora.Infrastructure.Backup;

/// <summary>A dumped column.</summary>
public sealed class BackupColumnManifest
{
    /// <summary>Gets or sets the column name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the store type (for example <c>nvarchar(200)</c>, <c>uuid</c>, <c>TEXT</c>).</summary>
    public string StoreType { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the column accepts nulls.</summary>
    public bool IsNullable { get; set; }

    /// <summary>Gets or sets the provider CLR type name (informational).</summary>
    public string? ClrType { get; set; }

    /// <summary>Gets or sets the ADO.NET parameter type of the column mapping, used to type parameters on restore.</summary>
    public DbType? DbType { get; set; }
}
