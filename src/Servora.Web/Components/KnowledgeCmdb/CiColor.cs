using System.Text.RegularExpressions;

namespace Servora.Web.Components.KnowledgeCmdb;

/// <summary>Validates administrator-defined class colors before they are written into style attributes.</summary>
public static partial class CiColor
{
    /// <summary>The fallback color.</summary>
    public const string Fallback = "#64748b";

    /// <summary>Returns the color when it is a strict <c>#rrggbb</c> value, otherwise <see cref="Fallback"/>.</summary>
    /// <param name="color">The color.</param>
    /// <returns>A safe color value.</returns>
    public static string Sanitize(string? color) => color is not null && HexRegex().IsMatch(color) ? color : Fallback;

    /// <summary>Gets the status color of an operational status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>A CSS color.</returns>
    public static string ForStatus(Servora.Domain.Cmdb.OperationalStatus status) => status switch
    {
        Servora.Domain.Cmdb.OperationalStatus.Operational => "#22c55e",
        Servora.Domain.Cmdb.OperationalStatus.NonOperational => "#f43f5e",
        Servora.Domain.Cmdb.OperationalStatus.Maintenance => "#f59e0b",
        Servora.Domain.Cmdb.OperationalStatus.Planned => "#3b82f6",
        _ => "#64748b",
    };

    /// <summary>Gets the edge color of a relationship type.</summary>
    /// <param name="type">The type.</param>
    /// <returns>A CSS color.</returns>
    public static string ForRelationship(Servora.Domain.Cmdb.CiRelationshipType type) => type switch
    {
        Servora.Domain.Cmdb.CiRelationshipType.DependsOn => "#8b7dff",
        Servora.Domain.Cmdb.CiRelationshipType.RunsOn => "#22d3ee",
        Servora.Domain.Cmdb.CiRelationshipType.HostedOn => "#3b82f6",
        Servora.Domain.Cmdb.CiRelationshipType.ConnectsTo => "#22c55e",
        Servora.Domain.Cmdb.CiRelationshipType.Contains => "#f59e0b",
        _ => "#94a3b8",
    };

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexRegex();
}
