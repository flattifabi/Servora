using Servora.Application.Tables;

namespace Servora.Application.Automation.Actions;

/// <summary>A configuration field of a step type.</summary>
/// <param name="Key">The configuration key.</param>
/// <param name="Label">The label.</param>
/// <param name="Kind">The editor type.</param>
public sealed record FlowActionField(string Key, string Label, FlowActionFieldKind Kind)
{
    /// <summary>Gets a value indicating whether a value is mandatory.</summary>
    public bool Required { get; init; }

    /// <summary>Gets the help text.</summary>
    public string? Help { get; init; }

    /// <summary>Gets the placeholder.</summary>
    public string? Placeholder { get; init; }

    /// <summary>Gets the default value.</summary>
    public string? DefaultValue { get; init; }

    /// <summary>Gets the options of <see cref="FlowActionFieldKind.Select"/> fields.</summary>
    public IReadOnlyList<FieldChoice> Options { get; init; } = [];

    /// <summary>
    /// Gets the key of the field this field depends on (the table for record values / record conditions, the data
    /// source for endpoint pickers).
    /// </summary>
    public string? TableFieldKey { get; init; }

    /// <summary>Gets a value indicating whether data pills can be inserted.</summary>
    public bool SupportsPills { get; init; } = true;
}
