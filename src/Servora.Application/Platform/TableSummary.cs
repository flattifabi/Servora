namespace Servora.Application.Platform;

/// <summary>A table as listed in the studio.</summary>
/// <param name="Name">The technical name.</param>
/// <param name="Label">The singular label.</param>
/// <param name="PluralLabel">The plural label.</param>
/// <param name="Icon">The icon name.</param>
/// <param name="IsCustom">Whether the table is a custom table.</param>
/// <param name="FieldCount">The number of fields.</param>
/// <param name="CustomFieldCount">The number of custom fields.</param>
/// <param name="SupportsCustomFields">Whether custom fields can be added.</param>
/// <param name="Route">The list route.</param>
public sealed record TableSummary(string Name, string Label, string PluralLabel, string Icon, bool IsCustom, int FieldCount, int CustomFieldCount, bool SupportsCustomFields, string? Route);
