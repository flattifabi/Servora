namespace Servora.Application.Automation.Model;

/// <summary>An input of a flow, supplied on manual starts, test runs and sub-flow calls (<c>{{inputs.&lt;name&gt;}}</c>).</summary>
public sealed class FlowInputDefinition
{
    /// <summary>Gets or sets the identifier (letters, digits, underscore).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the display label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the type: <c>string</c>, <c>number</c>, <c>boolean</c> or <c>record</c> (a record id).</summary>
    public string Type { get; set; } = "string";

    /// <summary>Gets or sets a value indicating whether the input is mandatory.</summary>
    public bool Required { get; set; }

    /// <summary>Gets or sets the default value.</summary>
    public string? DefaultValue { get; set; }
}
