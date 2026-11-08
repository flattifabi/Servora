using Servora.Application.Portal;
using Servora.Application.Security;

namespace Servora.Tests.Unit.Portal;

/// <summary>Self-service users opening <c>/</c> land in the portal; fulfillers and administrators stay on My Work.</summary>
public sealed class PortalLandingPolicyTests
{
    [Fact]
    public void Employee_with_portal_and_knowledge_lands_in_the_portal() =>
        Assert.True(PortalLandingPolicy.ShouldLandInPortal(Set(Permissions.Portal.Access, Permissions.Knowledge.Read, Permissions.Ai.UseAssistant)));

    [Fact]
    public void Portal_only_user_lands_in_the_portal() =>
        Assert.True(PortalLandingPolicy.ShouldLandInPortal(Set(Permissions.Portal.Access)));

    [Theory]
    [InlineData("incident.read")]
    [InlineData("problem.read")]
    [InlineData("change.read")]
    [InlineData("request.read")]
    [InlineData("incident.*")]
    public void Fulfiller_stays_on_my_work(string workspacePermission) =>
        Assert.False(PortalLandingPolicy.ShouldLandInPortal(Set(Permissions.Portal.Access, workspacePermission)));

    [Fact]
    public void Administrator_stays_on_my_work() =>
        Assert.False(PortalLandingPolicy.ShouldLandInPortal(Set(Permissions.All)));

    [Fact]
    public void User_without_portal_access_is_not_redirected() =>
        Assert.False(PortalLandingPolicy.ShouldLandInPortal(Set(Permissions.Observability.ReadAudit)));

    private static HashSet<string> Set(params string[] permissions) => new(permissions, StringComparer.Ordinal);
}
