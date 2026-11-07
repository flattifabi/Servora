namespace Servora.Domain.Cmdb;

/// <summary>Display labels of <see cref="CiRelationshipType"/> in both reading directions.</summary>
public static class CiRelationshipTypes
{
    /// <summary>Gets the label read from the parent ("depends on").</summary>
    /// <param name="type">The type.</param>
    /// <returns>The label.</returns>
    public static string Forward(CiRelationshipType type) => type switch
    {
        CiRelationshipType.DependsOn => "Depends on",
        CiRelationshipType.RunsOn => "Runs on",
        CiRelationshipType.HostedOn => "Hosted on",
        CiRelationshipType.ConnectsTo => "Connects to",
        CiRelationshipType.Contains => "Contains",
        _ => type.ToString(),
    };

    /// <summary>Gets the label read from the child ("used by").</summary>
    /// <param name="type">The type.</param>
    /// <returns>The label.</returns>
    public static string Reverse(CiRelationshipType type) => type switch
    {
        CiRelationshipType.DependsOn => "Used by",
        CiRelationshipType.RunsOn => "Runs",
        CiRelationshipType.HostedOn => "Hosts",
        CiRelationshipType.ConnectsTo => "Connected by",
        CiRelationshipType.Contains => "Contained by",
        _ => type.ToString(),
    };
}
