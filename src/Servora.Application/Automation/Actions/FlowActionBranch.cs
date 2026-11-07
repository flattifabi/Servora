namespace Servora.Application.Automation.Actions;

/// <summary>A branch (lane) offered by a step type.</summary>
/// <param name="Key">The branch key returned by the action.</param>
/// <param name="Label">The lane label.</param>
/// <param name="Tone">The visual tone (success, danger, info, warning, neutral).</param>
public sealed record FlowActionBranch(string Key, string Label, string Tone);
