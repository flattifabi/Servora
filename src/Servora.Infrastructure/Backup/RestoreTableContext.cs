namespace Servora.Infrastructure.Backup;

/// <summary>Provider-specific state while rows of one table are inserted (identity handling).</summary>
/// <param name="QuotedTable">The delimited, schema-qualified table name.</param>
/// <param name="QualifiedName">The table name as a string literal value for catalog functions (passed as a parameter).</param>
/// <param name="Delimit">Delimits (quotes) an identifier with the provider's <c>ISqlGenerationHelper</c>.</param>
internal sealed record RestoreTableContext(string QuotedTable, string QualifiedName, Func<string, string> Delimit)
{
    /// <summary>Gets or sets text inserted between the column list and <c>VALUES</c> (PostgreSQL <c>OVERRIDING SYSTEM VALUE</c>).</summary>
    public string InsertModifier { get; set; } = string.Empty;

    /// <summary>Gets the identity / serial columns whose generator must be reset after the insert.</summary>
    public List<string> GeneratedColumns { get; } = [];

    /// <summary>Gets or sets a value indicating whether SQL Server <c>IDENTITY_INSERT</c> was switched on.</summary>
    public bool IdentityInsertEnabled { get; set; }
}
