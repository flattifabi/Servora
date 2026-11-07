using Servora.Application.Ai;
using Servora.Application.Ai.Agents;
using Servora.Application.Security;

namespace Servora.Tests.Unit.Ai;

/// <summary>Tests for <see cref="AiPermissionIntersection"/> and <see cref="AiHeuristicPlanner"/>.</summary>
public sealed class AiPermissionIntersectionTests
{
    [Fact]
    public void Intersect_NeverGrantsWhatTheUserLacks()
    {
        var effective = AiPermissionIntersection.Intersect(
            [Permissions.Incidents.Read, Permissions.Incidents.Create],
            new HashSet<string> { Permissions.Incidents.Read, Permissions.Changes.Read });

        Assert.Equal([Permissions.Incidents.Read], effective);
    }

    [Fact]
    public void Intersect_AdminUserGetsExactlyTheAgentScope()
    {
        var effective = AiPermissionIntersection.Intersect([Permissions.Knowledge.Read], new HashSet<string> { Permissions.All });

        Assert.Equal([Permissions.Knowledge.Read], effective);
        Assert.DoesNotContain(Permissions.All, effective);
    }

    [Fact]
    public void Intersect_WildcardScopeOnlyPicksTheUsersConcretePermissions()
    {
        var effective = AiPermissionIntersection.Intersect(["incident.*"], new HashSet<string> { Permissions.Incidents.Read, Permissions.Changes.Read });

        Assert.Equal([Permissions.Incidents.Read], effective);
    }

    [Fact]
    public void Intersect_UserWithoutPermissionsGetsNothing()
    {
        Assert.Empty(AiPermissionIntersection.Intersect([Permissions.Knowledge.Read], new HashSet<string>()));
    }

    [Theory]
    [InlineData("How do I reset my VPN password?", "knowledge")]
    [InlineData("Show me INC0001001", "records")]
    [InlineData("The ERP is down and crashes with an error", "triage")]
    [InlineData("Hello there", null)]
    public void Planner_RoutesByKeywords(string message, string? expectedAgent)
    {
        IAiAgent[] agents = [new TriageAgent(), new KnowledgeAgent(), new RecordAgent(), new AutomationAgent()];

        Assert.Equal(expectedAgent, AiHeuristicPlanner.Choose(message, agents)?.Key);
    }

    [Fact]
    public void BuiltInAgents_HaveUniqueKeysAndBoundedScopes()
    {
        IAiAgent[] agents = [new TriageAgent(), new KnowledgeAgent(), new RecordAgent(), new AutomationAgent()];

        Assert.Equal(agents.Length, agents.Select(a => a.Key).Distinct().Count());
        Assert.All(agents, a => Assert.DoesNotContain(Permissions.All, a.Scope));
        Assert.All(agents, a => Assert.All(a.Scope.Where(p => !p.EndsWith(".*", StringComparison.Ordinal)), p => Assert.True(PermissionCatalog.IsKnown(p), p)));
    }
}
