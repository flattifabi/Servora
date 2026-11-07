namespace Servora.Application.Configuration;

/// <summary>
/// Describes a typed system setting: key, category, type, default and validation constraints. Definitions are
/// contributed by <see cref="ISettingDefinitionProvider"/>s; values are stored in <c>sys_properties</c> only when they
/// were changed by an administrator.
/// </summary>
/// <param name="Key">The unique dotted key, for example <c>branding.product_name</c>.</param>
/// <param name="Category">The category (see <see cref="SettingCategories"/>).</param>
/// <param name="Label">The display label.</param>
/// <param name="ValueType">The value type.</param>
/// <param name="DefaultValue">The normalized default value.</param>
public sealed record SettingDefinition(string Key, string Category, string Label, SettingValueType ValueType, string? DefaultValue)
{
    /// <summary>Gets the help text shown below the editor.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the allowed values for <see cref="SettingValueType.Choice"/>.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>Gets the minimum value for <see cref="SettingValueType.Integer"/>.</summary>
    public int? Min { get; init; }

    /// <summary>Gets the maximum value for <see cref="SettingValueType.Integer"/>.</summary>
    public int? Max { get; init; }

    /// <summary>Gets the maximum text length. Defaults to 500 (4000 for multi-line text).</summary>
    public int? MaxLength { get; init; }

    /// <summary>Gets a value indicating whether an empty value is rejected.</summary>
    public bool Required { get; init; }

    /// <summary>Gets a value indicating whether a change only takes effect after a service restart.</summary>
    public bool RequiresRestart { get; init; }

    /// <summary>Gets a value indicating whether the setting is managed internally and hidden from generic editors.</summary>
    public bool IsHidden { get; init; }

    /// <summary>Gets the sort order within the category.</summary>
    public int Order { get; init; }

    /// <summary>Gets an optional placeholder for the editor.</summary>
    public string? Placeholder { get; init; }

    /// <summary>Gets the effective maximum length for text values.</summary>
    public int EffectiveMaxLength => MaxLength ?? (ValueType == SettingValueType.MultilineText ? 4000 : 500);
}
