namespace Servora.Application.Integration.Imports;

/// <summary>One mapping of a transform map: source path (column or JSON path) → target field.</summary>
public sealed class ImportFieldMap
{
    /// <summary>Gets or sets the source column name or JSON path (for example <c>address.city</c>).</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the target table field.</summary>
    public string TargetField { get; set; } = string.Empty;

    /// <summary>Gets or sets the transform: <c>none</c>, <c>trim</c>, <c>upper</c>, <c>lower</c>.</summary>
    public string Transform { get; set; } = "trim";

    /// <summary>Gets or sets the value used when the source is empty.</summary>
    public string? DefaultValue { get; set; }
}
