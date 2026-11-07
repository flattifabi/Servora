namespace Servora.Web.Components.Automation;

/// <summary>Editable row of the <c>KeyValueEditor</c>.</summary>
public sealed class KeyValueRow
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the value.</summary>
    public string? Value { get; set; }
}
