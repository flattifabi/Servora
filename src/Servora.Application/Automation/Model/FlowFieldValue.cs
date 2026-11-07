namespace Servora.Application.Automation.Model;

/// <summary>A field assignment of a create / update record step (<c>Field = template</c>).</summary>
public sealed class FlowFieldValue
{
    /// <summary>Gets or sets the target field name.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Gets or sets the value template.</summary>
    public string? Value { get; set; }
}
