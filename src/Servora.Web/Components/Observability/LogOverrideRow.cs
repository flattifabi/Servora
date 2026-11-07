using Microsoft.Extensions.Logging;

namespace Servora.Web.Components.Observability;

/// <summary>Editable row of the category override list in the debug switch panel.</summary>
public sealed class LogOverrideRow
{
    /// <summary>Gets or sets the category prefix.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Gets or sets the minimum level.</summary>
    public LogLevel Level { get; set; } = LogLevel.Debug;
}
