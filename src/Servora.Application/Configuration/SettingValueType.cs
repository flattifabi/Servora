namespace Servora.Application.Configuration;

/// <summary>The data type of a system setting. Determines validation, normalization and the editor shown in the UI.</summary>
public enum SettingValueType
{
    /// <summary>Single-line text.</summary>
    String = 0,

    /// <summary>Multi-line text.</summary>
    MultilineText = 1,

    /// <summary>32-bit integer, optionally bounded by <see cref="SettingDefinition.Min"/> / <see cref="SettingDefinition.Max"/>.</summary>
    Integer = 2,

    /// <summary>Boolean stored as <c>true</c> / <c>false</c>.</summary>
    Boolean = 3,

    /// <summary>Hex color in the form <c>#rrggbb</c>.</summary>
    Color = 4,

    /// <summary>One of <see cref="SettingDefinition.Choices"/>.</summary>
    Choice = 5,

    /// <summary>E-mail address.</summary>
    Email = 6,

    /// <summary>Absolute <c>http</c> / <c>https</c> URL.</summary>
    Url = 7,

    /// <summary>Time zone identifier known to the operating system (Windows or IANA id).</summary>
    TimeZone = 8,
}
