using Servora.Domain.Common;

namespace Servora.Domain.Configuration;

/// <summary>
/// A system property (key/value configuration entry) managed through the administration UI. Secrets are never
/// stored here; use <see cref="StoredSecret"/> instead.
/// </summary>
public class SystemSetting : AuditableEntity
{
    /// <summary>Gets or sets the unique dotted key, for example <c>servora.branding.product_name</c>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the serialized value.</summary>
    public string? Value { get; set; }

    /// <summary>Gets or sets the category used to group settings in the UI.</summary>
    public string Category { get; set; } = "General";

    /// <summary>Gets or sets the description shown in the UI.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the value type hint: <c>string</c>, <c>int</c>, <c>bool</c>, <c>json</c>, <c>choice</c>, <c>color</c>.</summary>
    public string ValueType { get; set; } = "string";

    /// <summary>Gets or sets a value indicating whether a change requires a service restart.</summary>
    public bool RequiresRestart { get; set; }
}
