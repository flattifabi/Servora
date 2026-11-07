namespace Servora.Application.Events;

/// <summary>A single changed field of a record.</summary>
/// <param name="Field">The property / field name.</param>
/// <param name="OldValue">The previous value.</param>
/// <param name="NewValue">The new value.</param>
public sealed record RecordFieldChange(string Field, object? OldValue, object? NewValue);
