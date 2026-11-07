using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb;

/// <summary>Display helpers for CMDB values.</summary>
public static class CmdbDisplay
{
    /// <summary>Gets the label of an operational status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The label.</returns>
    public static string Status(OperationalStatus status) => status switch
    {
        OperationalStatus.Operational => "Operational",
        OperationalStatus.NonOperational => "Non-operational",
        OperationalStatus.Planned => "Planned",
        OperationalStatus.Maintenance => "Maintenance",
        OperationalStatus.Retired => "Retired",
        _ => status.ToString(),
    };

    /// <summary>Gets the design system tone of an operational status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The tone.</returns>
    public static string StatusTone(OperationalStatus status) => status switch
    {
        OperationalStatus.Operational => "success",
        OperationalStatus.NonOperational => "danger",
        OperationalStatus.Planned => "info",
        OperationalStatus.Maintenance => "warning",
        _ => "neutral",
    };

    /// <summary>Gets the label of a criticality value (1 = most critical).</summary>
    /// <param name="criticality">The criticality.</param>
    /// <returns>The label.</returns>
    public static string Criticality(int criticality) => criticality switch
    {
        1 => "1 – Mission critical",
        2 => "2 – High",
        3 => "3 – Medium",
        _ => "4 – Low",
    };
}
