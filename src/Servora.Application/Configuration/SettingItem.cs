namespace Servora.Application.Configuration;

/// <summary>A setting with its definition and current (effective) value, as shown on the settings page.</summary>
/// <param name="Definition">The definition.</param>
/// <param name="Value">The effective value (stored value or default).</param>
/// <param name="IsDefault">Whether no value is stored and the default applies.</param>
/// <param name="UpdatedAt">When the stored value was last changed.</param>
/// <param name="UpdatedBy">Who last changed the stored value.</param>
public sealed record SettingItem(SettingDefinition Definition, string Value, bool IsDefault, DateTimeOffset? UpdatedAt, string? UpdatedBy)
{
    /// <summary>Gets the setting key.</summary>
    public string Key => Definition.Key;

    /// <summary>Gets a value indicating whether this is a feature flag defined by an administrator (not by a module).</summary>
    public bool IsCustom { get; init; }
}
