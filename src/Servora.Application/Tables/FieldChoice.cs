namespace Servora.Application.Tables;

/// <summary>One selectable value of a <see cref="FieldType.Choice"/> field.</summary>
/// <param name="Value">The stored value (enum numeric value or choice key).</param>
/// <param name="Label">The display label.</param>
public sealed record FieldChoice(string Value, string Label);
