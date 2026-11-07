namespace Servora.Application.Automation.Actions;

/// <summary>An output of a step type, offered as data pill <c>steps.&lt;key&gt;.&lt;output&gt;</c> to later steps.</summary>
/// <param name="Key">The output key.</param>
/// <param name="Label">The label.</param>
/// <param name="TableFieldKey">When set, the output is a record of the table configured in this field; the pill picker offers its fields.</param>
public sealed record FlowActionOutput(string Key, string Label, string? TableFieldKey = null);
