namespace Servora.Infrastructure.Backup;

/// <summary>A foreign key constraint of a backed-up table.</summary>
public sealed class BackupForeignKeyManifest
{
    /// <summary>Gets or sets the constraint name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the dependent columns.</summary>
    public List<string> Columns { get; set; } = [];

    /// <summary>Gets or sets the principal table name.</summary>
    public string PrincipalTable { get; set; } = string.Empty;

    /// <summary>Gets or sets the principal table schema.</summary>
    public string? PrincipalSchema { get; set; }

    /// <summary>Gets or sets the principal columns.</summary>
    public List<string> PrincipalColumns { get; set; } = [];

    /// <summary>Gets the key of the principal table.</summary>
    public string PrincipalKey => BackupTableManifest.TableKey(PrincipalSchema, PrincipalTable);
}
